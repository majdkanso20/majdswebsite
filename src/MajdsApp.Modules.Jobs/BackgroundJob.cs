using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Jobs;

public enum BackgroundJobStatus { Pending = 0, Running = 1, Succeeded = 2, Failed = 3 }

/// <summary>A queued one-off job (F-Background-Jobs FR-JOB-001/002/004). Persisted, so it survives a restart; the user who
/// queued it travels with it.</summary>
public class BackgroundJob : MajdsApp.SharedKernel.Data.IEntity<Guid>
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Parameters { get; set; } = "{}";
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public BackgroundJobStatus Status { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Not started before this time: the requested delay, later the retry backoff.</summary>
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");
        builder.Property(j => j.Type).HasMaxLength(100);
        builder.Property(j => j.Parameters).HasMaxLength(4000);
        builder.Property(j => j.UserId).HasMaxLength(450);
        builder.Property(j => j.UserName).HasMaxLength(256);
        builder.Property(j => j.LastError).HasMaxLength(1000);
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });
        builder.HasIndex(j => j.CreatedAt);
    }
}
