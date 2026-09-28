using MajdsApp.SharedKernel.Files;
using MajdsApp.Data;
using MajdsApp.Modules.Files;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Exports;

/// <summary>Deletes finished exports, and the files they produced, once they are older than the retention setting, so
/// generated files do not pile up on disk.</summary>
public class ExportsCleanupJob(ApplicationDbContext db, IFileStorage storage, ISettingsProvider settings) : IRecurringJob
{
    public string Name => "Export cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var days = Math.Max(1, await settings.GetIntegerAsync("Exports.RetentionDays", ct));
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var expired = await db.Set<ExportJob>()
            .Where(j => j.CreatedAt < cutoff && j.Status != ExportJobStatus.Pending && j.Status != ExportJobStatus.Running)
            .ToListAsync(ct);

        foreach (var job in expired)
        {
            if (job.FileId is { } fileId)
            {
                var record = await db.Set<FileRecord>().FirstOrDefaultAsync(f => f.Id == fileId, ct);
                if (record is not null)
                {
                    storage.Delete(record.StoredName);
                    db.Set<FileRecord>().Remove(record);
                }
            }

            db.Set<ExportJob>().Remove(job);
        }

        if (expired.Count > 0) await db.SaveChangesAsync(ct);
    }
}
