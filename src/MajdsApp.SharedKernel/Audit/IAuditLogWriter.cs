namespace MajdsApp.SharedKernel.Audit;

/// <summary>One property that changed on an entity, with sensitive values already redacted (FR-AUDIT-002/004).</summary>
public record AuditPropertyChange(string Name, string? Original, string? New);

/// <summary>One entity that was created, updated or deleted during an audited action (FR-AUDIT-002).</summary>
public record AuditChange(string EntityType, string EntityId, string ChangeType, IReadOnlyList<AuditPropertyChange> Properties);

/// <summary>Everything recorded about one audited action (FR-AUDIT-001).</summary>
public record AuditRecord(
    string Action,
    string? UserId,
    string? UserName,
    string? HttpMethod,
    string? Url,
    int DurationMs,
    string? ClientIp,
    string? ClientBrowser,
    bool Succeeded,
    string Outcome,
    string? Error,
    string? Parameters,
    IReadOnlyList<AuditChange> Changes);

/// <summary>
/// Records an audited action (F-Audit). The Shared Kernel ships a no-op default so
/// <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> can depend on this before F-Audit exists;
/// the F-Audit module replaces this registration with the real database-backed one (same pattern as
/// <see cref="Security.IPermissionChecker"/> and <see cref="Settings.ISettingsProvider"/>).
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>Writes a successful action on the request's own database context, so it commits or rolls back with the change it describes.</summary>
    Task WriteAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>Writes a failed or refused action on a separate context. The request's own transaction has
    /// been (or is about to be) rolled back, and the failure must be recorded regardless.</summary>
    Task WriteFailureAsync(AuditRecord record, CancellationToken ct = default);
}

public class NullAuditLogWriter : IAuditLogWriter
{
    public Task WriteAsync(AuditRecord record, CancellationToken ct = default) => Task.CompletedTask;
    public Task WriteFailureAsync(AuditRecord record, CancellationToken ct = default) => Task.CompletedTask;
}
