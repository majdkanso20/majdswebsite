using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Files;

/// <summary>Metadata for one stored file (F-Files data model). The bytes live on disk under a
/// server-generated name; the client-supplied name is kept only here, never used as a path.</summary>
public class FileRecord
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StoredName { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> builder)
    {
        builder.ToTable("Files");
        builder.Property(f => f.FileName).HasMaxLength(260);
        builder.Property(f => f.ContentType).HasMaxLength(128);
        builder.Property(f => f.StoredName).HasMaxLength(64);
        builder.HasIndex(f => f.OwnerId);
    }
}
