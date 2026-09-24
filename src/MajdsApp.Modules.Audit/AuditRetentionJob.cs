using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

public static class AuditSettings
{
    public static class Audit
    {
        public static readonly SettingDefinition RetentionDays = new(
            "Audit.RetentionDays", "Audit", "Audit log retention (days)", SettingDataType.Integer, "90",
            description: "Audit entries older than this are deleted daily. 0 keeps everything.");
    }
}

/// <summary>Daily purge of audit entries past the configured retention window.</summary>
public class AuditRetentionJob(ApplicationDbContext db, ISettingsProvider settings) : IRecurringJob
{
    public string Name => "Audit log retention";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var days = await settings.GetIntegerAsync("Audit.RetentionDays", ct);
        if (days <= 0)
            return;

        var cutoff = DateTime.UtcNow.AddDays(-days);
        await db.Set<AuditLogEntry>().Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
