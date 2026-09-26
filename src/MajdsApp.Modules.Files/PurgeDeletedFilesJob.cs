using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Files;

/// <summary>
/// Physically removes files that were deleted more than <c>Files.DeletedRetentionDays</c> days ago: the bytes first, then the record (P F-Files FR-FILE-006).
/// If removing the bytes fails the record is kept, so the next run tries again instead of leaving an untracked file on disk.
/// </summary>
public class PurgeDeletedFilesJob(ApplicationDbContext db, FileStorage storage, ISettingsProvider settings) : IRecurringJob
{
    public string Name => "Deleted file cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var days = Math.Max(1, await settings.GetIntegerAsync("Files.DeletedRetentionDays", ct));
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var expired = await db.Set<FileRecord>().IgnoreQueryFilters().Where(f => f.DeletedAt != null && f.DeletedAt < cutoff).ToListAsync(ct);

        foreach (var record in expired)
        {
            try
            {
                storage.Delete(record.StoredName);
            }
            catch (IOException)
            {
                continue; // still in use or locked: keep the record and try again on the next run
            }

            db.Set<FileRecord>().Remove(record);
        }

        if (expired.Count > 0) await db.SaveChangesAsync(ct);
    }
}
