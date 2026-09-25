namespace MajdsApp.SharedKernel.Export;

/// <summary>
/// A dataset that can be exported (F-Export). A module registers one (<c>services.AddScoped&lt;IExportSource, MySource&gt;()</c>) and
/// gets, with no further code, both its immediate download and background export (FR-EXP-004). The source only builds
/// the file from <paramref name="filters"/>; who may export it is decided by <see cref="Permission"/> before the job is queued.
/// </summary>
public interface IExportSource
{
    /// <summary>Stable identifier used by the API, for example <c>users</c>.</summary>
    string Key { get; }

    /// <summary>Human title shown in the exports list, for example <c>Users</c>.</summary>
    string Title { get; }

    /// <summary>Permission needed to export this dataset (the resource's view permission); null = any signed-in user.</summary>
    string? Permission { get; }

    /// <summary>Builds the file. <paramref name="filters"/> are the same filters the list accepts; unknown keys are ignored.
    /// Runs without an HTTP request when it is a background job, so it must not depend on the current user.</summary>
    Task<ExportFile> BuildAsync(ExportFormat format, IReadOnlyDictionary<string, string> filters, int maxRows, CancellationToken ct);
}
