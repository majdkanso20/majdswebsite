using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Imports;

public enum ImportJobStatus { Pending = 0, Running = 1, Completed = 2, Failed = 3 }

/// <summary>
/// A queued or finished background import (F-Export FR-EXP-003/004): the uploaded file is held here until the worker
/// runs it, then the per-row outcome (how many rows went in, and for the rest, the row and why) is kept for the history
/// list. The file itself never leaves this row — imports are capped small enough (<see cref="MajdsApp.SharedKernel.Import.TabularReader.MaxBytes"/>)
/// that there is no need for F-Files the way a generated export's output needs it.
/// </summary>
public class ImportJob : MajdsApp.SharedKernel.Data.IEntity<Guid>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }

    /// <summary>The <c>IImportSource.Key</c> that ran this import, for example <c>users</c>.</summary>
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];

    public ImportJobStatus Status { get; set; }
    public int Total { get; set; }
    public int Succeeded { get; set; }

    /// <summary>Per-row failures, as JSON (<c>ImportRowError[]</c>): the row number and why.</summary>
    public string? Errors { get; set; }

    /// <summary>A whole-file problem (wrong type, missing column) rather than a per-row one.</summary>
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");
        builder.Property(j => j.UserId).HasMaxLength(450);
        builder.Property(j => j.UserName).HasMaxLength(256);
        builder.Property(j => j.Source).HasMaxLength(50);
        builder.Property(j => j.Title).HasMaxLength(100);
        builder.Property(j => j.FileName).HasMaxLength(260);
        builder.Property(j => j.Error).HasMaxLength(500);
        builder.Property(j => j.Errors).HasMaxLength(20_000);
        builder.HasIndex(j => new { j.UserId, j.CreatedAt });
        builder.HasIndex(j => new { j.Status, j.CreatedAt });
    }
}
