using MajdsApp.Data;
using MajdsApp.SharedKernel.Jobs;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Files;

/// <summary>
/// Temp-file cleanup (F-Background-Jobs FR-JOB-005): deletes stored files that no file record points to (a soft-deleted file still has its record, so it is not an orphan). An upload that crashed
/// between writing the bytes and saving the record leaves one. Only files older than a day are touched, so a file that is being
/// uploaded right now is never removed.
/// </summary>
public class OrphanedFilesCleanupJob(ApplicationDbContext db, FileStorage storage) : IRecurringJob
{
    public static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public string Name => "Orphaned file cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - MinimumAge;
        var candidates = storage.Enumerate().Where(f => f.LastWriteUtc < cutoff).Select(f => f.Name).ToList();
        if (candidates.Count == 0) return;

        var known = new HashSet<string>();
        foreach (var batch in candidates.Chunk(500))
            foreach (var name in await db.Set<FileRecord>().IgnoreQueryFilters().AsNoTracking().Where(f => batch.Contains(f.StoredName)).Select(f => f.StoredName).ToListAsync(ct))
                known.Add(name);

        foreach (var name in candidates.Where(n => !known.Contains(n)))
            storage.Delete(name);
    }
}
