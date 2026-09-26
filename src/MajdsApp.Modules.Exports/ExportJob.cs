using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Exports;

public enum ExportJobStatus { Pending = 0, Running = 1, Completed = 2, Failed = 3 }

/// <summary>A queued or finished background export (F-Export FR-EXP-004). The finished file lives in F-Files and
/// belongs to the user who asked for it.</summary>
public class ExportJob : MajdsApp.SharedKernel.Data.IEntity<Guid>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }

    /// <summary>The <c>IExportSource.Key</c> being exported, for example <c>users</c>.</summary>
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>csv or xlsx.</summary>
    public string Format { get; set; } = "csv";

    /// <summary>The list filters the export was started with, as JSON, so the file holds exactly what the user was looking at (AC-EXP-1).</summary>
    public string Filters { get; set; } = "{}";

    public ExportJobStatus Status { get; set; }
    public Guid? FileId { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("ExportJobs");
        builder.Property(j => j.UserId).HasMaxLength(450);
        builder.Property(j => j.UserName).HasMaxLength(256);
        builder.Property(j => j.Source).HasMaxLength(50);
        builder.Property(j => j.Title).HasMaxLength(100);
        builder.Property(j => j.Format).HasMaxLength(10);
        builder.Property(j => j.Filters).HasMaxLength(2000);
        builder.Property(j => j.Error).HasMaxLength(500);
        builder.HasIndex(j => new { j.UserId, j.CreatedAt });
        builder.HasIndex(j => new { j.Status, j.CreatedAt });
    }
}
