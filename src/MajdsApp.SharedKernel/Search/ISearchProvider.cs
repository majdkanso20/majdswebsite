namespace MajdsApp.SharedKernel.Search;

/// <param name="Route">SPA route to open; a "q" query parameter pre-filters the target list.</param>
public record SearchResult(string Category, string Title, string? Subtitle, string Route);

/// <summary>
/// A module's contribution to global search (F-Search). Implement this in the module that owns the data
/// and it's discovered and registered by convention; the search endpoint runs only the providers the
/// caller is allowed to use, so a provider never has to think about who is asking — except to narrow
/// results within what it exposes (e.g. "only my own files").
/// </summary>
public interface ISearchProvider
{
    string Category { get; }

    /// <summary>Permission the caller needs for this provider to run at all; null = any signed-in user.</summary>
    string? RequiredPermission { get; }

    /// <summary>Feature flag that must be on for this provider to run; null = always.</summary>
    string? RequiredFeature { get; }

    Task<IReadOnlyList<SearchResult>> SearchAsync(string term, int max, CancellationToken ct);
}
