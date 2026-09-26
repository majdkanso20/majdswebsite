namespace MajdsApp.SharedKernel.Paging;

/// <summary>Standard paged-list request contract (FR-GRID-001) used by every list endpoint.</summary>
public class PagedRequest
{
    /// <summary>The largest page a client can ask for (FR-GRID-004). Set at startup from <c>Paging:MaxPageSize</c> (default 100).</summary>
    public static int MaxPageSize { get; set; } = 100;

    private int _pageSize = 20;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, Math.Max(1, MaxPageSize)); // FR-GRID-004
    }

    /// <summary>e.g. "name:asc" — validated against a column allow-list by the caller (NFR-SEC-3).</summary>
    public string? Sort { get; set; }

    /// <summary>Free-text search term.</summary>
    public string? Filter { get; set; }
}
