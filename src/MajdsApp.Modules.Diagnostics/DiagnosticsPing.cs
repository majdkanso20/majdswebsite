using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>A trivial entity owned entirely by this module — proves P2 (module owns its schema) and
/// P3 (generic repository/UoW) without touching any other module's data.</summary>
public class DiagnosticsPing : AuditableEntity<Guid>
{
    public required string Message { get; set; }
}

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
