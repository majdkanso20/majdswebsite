using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>A trivial entity owned entirely by this module — proves P2 (module owns its schema) and
/// P3 (generic repository/UoW) without touching any other module's data.</summary>
public class DiagnosticsPing : AuditableEntity<Guid>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    public required string Message { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    /// <summary>Says that this ping was recorded; published once the save that stores it succeeds (P3 FR-REPO-005).</summary>
    public void RecordedAs(string by) => _events.Add(new PingRecorded(Id, Message, by));
}

/// <summary>Raised when a ping is recorded. The domain-event mechanism's reference example.</summary>
public record PingRecorded(Guid PingId, string Message, string RecordedBy) : IDomainEvent;

public class DiagnosticsPingConfiguration : IEntityTypeConfiguration<DiagnosticsPing>
{
    public void Configure(EntityTypeBuilder<DiagnosticsPing> builder)
    {
        builder.ToTable("DiagnosticsPings");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Message).HasMaxLength(256).IsRequired();
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
