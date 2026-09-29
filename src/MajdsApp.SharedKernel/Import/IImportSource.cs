namespace MajdsApp.SharedKernel.Import;

/// <summary>
/// A resource that can be imported from a file in the background (F-Export FR-EXP-003/004), the import
/// counterpart to <see cref="Export.IExportSource"/>: any module registers one and gets a queued job, a
/// history list and per-row results for free through the Imports module, with no further code there.
/// </summary>
public interface IImportSource
{
    /// <summary>Stable identifier used by the API, for example <c>users</c>.</summary>
    string Key { get; }

    /// <summary>Human title shown in the import history, for example <c>Users</c>.</summary>
    string Title { get; }

    /// <summary>Permission needed to run this import (the resource's create permission); null = any signed-in user.</summary>
    string? Permission { get; }

    /// <summary>The blank template's columns (FR-EXP-005), for <c>GET /api/imports/{key}/template</c>.</summary>
    IReadOnlyList<ImportColumn> TemplateColumns { get; }

    /// <summary>
    /// Parses and imports the file, one row at a time (typically through <see cref="ImportRunner.RunAsync"/> calling the same
    /// command the resource's own create endpoint uses, so a background import obeys exactly the same validation).
    /// Runs without an HTTP request when it is a background job, so it must not depend on the current user beyond the
    /// audited command it delegates to.
    /// </summary>
    Task<ImportResult> RunAsync(byte[] content, string fileName, CancellationToken ct);
}
