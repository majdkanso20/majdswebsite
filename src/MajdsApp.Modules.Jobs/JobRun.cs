using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Jobs;

/// <summary>One execution of a recurring job (F-Background-Jobs data model).</summary>
public class JobRun
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Trigger { get; set; } = "Schedule";
}

public class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("JobRuns");
        builder.Property(j => j.JobName).HasMaxLength(200);
        builder.Property(j => j.Error).HasMaxLength(1000);
        builder.Property(j => j.Trigger).HasMaxLength(20);
        builder.HasIndex(j => new { j.JobName, j.StartedAt });
    }
}
