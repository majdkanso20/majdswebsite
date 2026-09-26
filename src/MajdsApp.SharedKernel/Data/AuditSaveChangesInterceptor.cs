using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MajdsApp.SharedKernel.Data;

/// <summary>
/// Centrally stamps <see cref="IAuditable"/> fields on every save (FR-REPO-005, cross-cutting standard
/// #10) so handlers never set CreatedAt/By or ModifiedAt/By themselves. It also (a) records property-level
/// before/after values of what a save changed, into the request's <see cref="IAuditChangeBuffer"/> for the
/// audit record to pick up (FR-AUDIT-002; sensitive values are redacted), and (b) refuses to modify or
/// delete audit rows so the trail is append-only (FR-AUDIT-005). Register on any DbContext via
/// <c>optionsBuilder.AddInterceptors(...)</c>.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser, IAuditChangeBuffer? changeBuffer = null, IPublisher? publisher = null) : SaveChangesInterceptor
{
    // The audit tables themselves, plus high-churn technical tables that would only add noise.
    private static readonly HashSet<string> NotTracked = ["AuditLogEntry", "EntityChange", "EntityPropertyChange", "Notification", "NotificationDelivery", "JobRun"];
    private static readonly HashSet<string> AppendOnly = ["AuditLogEntry", "EntityChange", "EntityPropertyChange"];

    private record Pending(EntityEntry Entry, string ChangeType, string? KeyBeforeSave, IReadOnlyList<AuditPropertyChange> Properties);
    private List<Pending> _pending = [];
    private List<IHasDomainEvents> _eventSources = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Prepare(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Prepare(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush();
        PublishEventsAsync(default).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush();
        await PublishEventsAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending = [];
        _eventSources = []; // the events stay on their entities, so a corrected retry still publishes them once
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = [];
        _eventSources = [];
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Prepare(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;
        var userId = currentUser.UserId;
        _pending = [];

        // Soft delete is enforced here, once, for every entity that supports it (FR-REPO-005): removing one marks it deleted instead of
        // deleting the row, whichever way the caller removed it (repository, DbSet or context).
        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>().Where(e => e.State == EntityState.Deleted).ToList())
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }

        _eventSources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.State != EntityState.Detached && e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity).ToList();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var typeName = entry.Metadata.ClrType.Name;
            if (AppendOnly.Contains(typeName) && entry.State != EntityState.Added)
                throw new InvalidOperationException("Audit records are append-only and cannot be modified or deleted.");

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.ModifiedAt = now;
                    auditable.ModifiedBy = userId;
                }
            }

            if (changeBuffer is null || NotTracked.Contains(typeName))
                continue;

            var properties = DescribeProperties(entry);
            if (entry.State == EntityState.Modified && properties.Count == 0)
                continue;

            var changeType = entry.State switch { EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated" };
            _pending.Add(new Pending(entry, changeType, entry.State == EntityState.Added ? null : KeyOf(entry), properties));
        }
    }

    // Domain events go out only after the save that stored the change has succeeded (FR-REPO-005). Handlers may save again, so the lists are taken first.
    private async Task PublishEventsAsync(CancellationToken ct)
    {
        var sources = _eventSources;
        _eventSources = [];
        var events = sources.SelectMany(s => s.DomainEvents).ToList();
        foreach (var source in sources) source.ClearDomainEvents();

        if (publisher is null) return;
        foreach (var domainEvent in events)
            await publisher.Publish(domainEvent, ct);
    }

    // Ids generated by the database only exist after the save, so the key is read here for new rows.
    private void Flush()
    {
        var pending = _pending;
        _pending = [];
        if (changeBuffer is null) return;

        foreach (var p in pending)
            changeBuffer.Add(new AuditChange(p.Entry.Metadata.ClrType.Name, p.KeyBeforeSave ?? KeyOf(p.Entry), p.ChangeType, p.Properties));
    }

    private static string KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        return key is null ? string.Empty : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
    }

    private static List<AuditPropertyChange> DescribeProperties(EntityEntry entry)
    {
        var changes = new List<AuditPropertyChange>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name == "ConcurrencyStamp") continue; // changes on every save; carries no information
            var original = entry.State == EntityState.Added ? null : property.OriginalValue?.ToString();
            var current = entry.State == EntityState.Deleted ? null : property.CurrentValue?.ToString();

            if (entry.State == EntityState.Modified && (!property.IsModified || original == current))
                continue;
            if (entry.State == EntityState.Added && current is null)
                continue;

            changes.Add(new AuditPropertyChange(name, AuditRedactor.Redact(name, original), AuditRedactor.Redact(name, current)));
        }

        return changes;
    }
}
