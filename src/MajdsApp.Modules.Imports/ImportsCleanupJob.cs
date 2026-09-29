using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Imports;

/// <summary>Deletes finished import jobs once they are older than the retention setting, so import history
/// (and any leftover uploaded file content) does not pile up.</summary>
public class ImportsCleanupJob(ApplicationDbContext db, ISettingsProvider settings) : IRecurringJob
{
    public string Name => "Import cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var days = Math.Max(1, await settings.GetIntegerAsync("Imports.RetentionDays", ct));
        var cutoff = DateTime.UtcNow.AddDays(-days);

        await db.Set<ImportJob>()
            .Where(j => j.CreatedAt < cutoff && j.Status != ImportJobStatus.Pending && j.Status != ImportJobStatus.Running)
            .ExecuteDeleteAsync(ct);
    }
}
