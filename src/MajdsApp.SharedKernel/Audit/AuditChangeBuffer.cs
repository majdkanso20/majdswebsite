namespace MajdsApp.SharedKernel.Audit;

/// <summary>Collects the entity changes saved during the current request so the audit record for the
/// action can attach them (FR-AUDIT-002). Filled by <c>AuditSaveChangesInterceptor</c>, drained by <c>AuditBehavior</c>.</summary>
public interface IAuditChangeBuffer
{
    void Add(AuditChange change);
    IReadOnlyList<AuditChange> Drain();
}

public class AuditChangeBuffer : IAuditChangeBuffer
{
    private readonly List<AuditChange> _changes = [];

    public void Add(AuditChange change) => _changes.Add(change);

    public IReadOnlyList<AuditChange> Drain()
    {
        var snapshot = _changes.ToList();
        _changes.Clear();
        return snapshot;
    }
}
