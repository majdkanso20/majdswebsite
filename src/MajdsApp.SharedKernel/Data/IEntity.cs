namespace MajdsApp.SharedKernel.Data;

public interface IEntity<TKey>
{
    TKey Id { get; }
}

/// <summary>Soft-delete marker (cross-cutting standard #10). Populated centrally in SaveChanges.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Audit-field marker (cross-cutting standard #10). Populated centrally in SaveChanges.
/// Uses UTC <see cref="DateTime"/> rather than <see cref="DateTimeOffset"/>: functionally equivalent
/// for audit purposes since values are always UTC, and avoids a SQLite/EF Core limitation where
/// ORDER BY on a DateTimeOffset column throws NotSupportedException (SQL Server/PostgreSQL don't
/// have this restriction, so this is a portable choice either way).
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
}

/// <summary>Convenience base for entities that want Id + soft-delete + audit fields together.</summary>
public abstract class AuditableEntity<TKey> : IEntity<TKey>, ISoftDelete, IAuditable
{
    public required TKey Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
