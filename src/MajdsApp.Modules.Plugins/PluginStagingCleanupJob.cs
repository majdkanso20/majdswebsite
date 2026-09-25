using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Plugins;

namespace MajdsApp.Modules.Plugins;

/// <summary>
/// Temp-file cleanup (F-Background-Jobs FR-JOB-005): removes plugin scratch folders (<c>.staging</c>) that were left behind, for
/// example by a process that stopped in the middle of an upload. Anything under a day old is left alone, since an upload may be
/// in progress. Staged changes waiting for the next start live elsewhere (<c>.pending</c>) and are never touched.
/// </summary>
public class PluginStagingCleanupJob(PluginHostOptions options) : IRecurringJob
{
    public static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public string Name => "Plugin staging cleanup";
    public TimeSpan Interval => TimeSpan.FromHours(24);

    public Task ExecuteAsync(CancellationToken ct)
    {
        var staging = Path.Combine(options.Directory, ".staging");
        if (!Directory.Exists(staging)) return Task.CompletedTask;

        var cutoff = DateTime.UtcNow - MinimumAge;
        foreach (var directory in Directory.GetDirectories(staging).Where(d => Directory.GetLastWriteTimeUtc(d) < cutoff))
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* still in use: try again tomorrow */ }
        }

        return Task.CompletedTask;
    }
}
