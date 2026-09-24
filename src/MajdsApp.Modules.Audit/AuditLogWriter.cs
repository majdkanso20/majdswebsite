using MajdsApp.Data;
using MajdsApp.SharedKernel.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Audit;

/// <summary>
/// Real F-Audit implementation, replacing SharedKernel's no-op default. A success is written on the
/// request's own context and always calls SaveChanges itself: for a transactional command,
/// <c>AuditBehavior</c> runs before <c>TransactionBehavior</c>'s commit, so this flushes into that same
/// still-open transaction (atomic with the change it describes); for a command backed by an ASP.NET
/// Identity store (which self-saves and has no ambient transaction), this is the only thing that would
/// ever persist the row. A failure or refusal is written on a fresh context in its own scope, because the
/// request's context may hold the rolled-back changes and its transaction must not decide whether the
/// failure is recorded.
/// </summary>
public class AuditLogWriter(ApplicationDbContext db, IServiceScopeFactory scopes) : IAuditLogWriter
{
    public async Task WriteAsync(AuditRecord record, CancellationToken ct = default)
    {
        db.Set<AuditLogEntry>().Add(ToEntity(record));
        await db.SaveChangesAsync(ct);
    }

    public async Task WriteFailureAsync(AuditRecord record, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var isolated = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        isolated.Set<AuditLogEntry>().Add(ToEntity(record));
        await isolated.SaveChangesAsync(ct);
    }

    private static AuditLogEntry ToEntity(AuditRecord r) => new()
    {
        Action = r.Action,
        UserId = r.UserId,
        UserName = r.UserName,
        CreatedAt = DateTime.UtcNow,
        HttpMethod = r.HttpMethod,
        Url = r.Url,
        DurationMs = r.DurationMs,
        ClientIp = r.ClientIp,
        ClientBrowser = r.ClientBrowser,
        Succeeded = r.Succeeded,
        Outcome = r.Outcome,
        Error = r.Error,
        Parameters = r.Parameters,
        Changes = r.Changes.Select(c => new EntityChange
        {
            EntityType = c.EntityType,
            EntityId = c.EntityId,
            ChangeType = c.ChangeType,
            Properties = c.Properties.Select(p => new EntityPropertyChange
            {
                PropertyName = p.Name,
                OriginalValue = p.Original,
                NewValue = p.New
            }).ToList()
        }).ToList()
    };
}
