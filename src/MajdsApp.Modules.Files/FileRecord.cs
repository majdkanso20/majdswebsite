using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Files;

/// <summary>Metadata for one stored file (F-Files data model). The bytes live on disk under a
/// server-generated name; the client-supplied name is kept only here, never used as a path.</summary>
public class FileRecord : MajdsApp.SharedKernel.Data.IEntity<Guid>
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StoredName { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Set when the file is deleted (FR-FILE-006). The record and the bytes are kept until the retention period passes and the cleanup job removes them;
    /// meanwhile the file is invisible to every query.</summary>
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> builder)
    {
        builder.ToTable("Files");
        builder.Property(f => f.FileName).HasMaxLength(260);
        builder.Property(f => f.ContentType).HasMaxLength(128);
        builder.Property(f => f.StoredName).HasMaxLength(64);
        builder.Property(f => f.DeletedBy).HasMaxLength(450);
        builder.HasIndex(f => f.OwnerId);
        builder.HasIndex(f => f.DeletedAt);

        // Soft delete: a deleted file is hidden from every query, so no list, download, export or profile picture can reach it by accident.
        // Code that must see them (the purge job, the orphan sweep) says so with IgnoreQueryFilters.
        builder.HasQueryFilter(f => f.DeletedAt == null);
    }
}
