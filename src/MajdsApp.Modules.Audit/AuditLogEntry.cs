using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Audit;

/// <summary>One audited action (F-Audit data model): who did what, from where, how long it took, whether
/// it worked, the redacted parameters, and the entity changes it caused. Append-only (FR-AUDIT-005) —
/// enforced by the save interceptor as well as by the absence of any modify/delete API.</summary>
public class AuditLogEntry : MajdsApp.SharedKernel.Data.IEntity<int>
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public int DurationMs { get; set; }
    public string? ClientIp { get; set; }
    public string? ClientBrowser { get; set; }
    public bool Succeeded { get; set; } = true;
    public string Outcome { get; set; } = "Success";
    public string? Error { get; set; }
    public string? Parameters { get; set; }

    public List<EntityChange> Changes { get; set; } = [];
}

public class EntityChange
{
    public int Id { get; set; }
    public int AuditLogEntryId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;

    public List<EntityPropertyChange> Properties { get; set; } = [];
}

public class EntityPropertyChange
{
    public int Id { get; set; }
    public int EntityChangeId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string? OriginalValue { get; set; }
    public string? NewValue { get; set; }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.Property(a => a.Action).HasMaxLength(256);
        builder.Property(a => a.UserName).HasMaxLength(256);
        builder.Property(a => a.HttpMethod).HasMaxLength(10);
        builder.Property(a => a.Url).HasMaxLength(500);
        builder.Property(a => a.ClientIp).HasMaxLength(64);
        builder.Property(a => a.ClientBrowser).HasMaxLength(300);
        builder.Property(a => a.Outcome).HasMaxLength(30).HasDefaultValue("Success");
        builder.Property(a => a.Succeeded).HasDefaultValue(true);
        builder.Property(a => a.Error).HasMaxLength(500);
        builder.Property(a => a.Parameters).HasMaxLength(4100);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasMany(a => a.Changes).WithOne().HasForeignKey(c => c.AuditLogEntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EntityChangeConfiguration : IEntityTypeConfiguration<EntityChange>
{
    public void Configure(EntityTypeBuilder<EntityChange> builder)
    {
        builder.ToTable("AuditEntityChanges");
        builder.Property(c => c.EntityType).HasMaxLength(100);
        builder.Property(c => c.EntityId).HasMaxLength(200);
        builder.Property(c => c.ChangeType).HasMaxLength(20);
        builder.HasMany(c => c.Properties).WithOne().HasForeignKey(p => p.EntityChangeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EntityPropertyChangeConfiguration : IEntityTypeConfiguration<EntityPropertyChange>
{
    public void Configure(EntityTypeBuilder<EntityPropertyChange> builder)
    {
        builder.ToTable("AuditPropertyChanges");
        builder.Property(p => p.PropertyName).HasMaxLength(100);
        builder.Property(p => p.OriginalValue).HasMaxLength(510);
        builder.Property(p => p.NewValue).HasMaxLength(510);
    }
}
