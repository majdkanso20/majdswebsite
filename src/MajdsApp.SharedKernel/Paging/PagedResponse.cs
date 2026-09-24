namespace MajdsApp.SharedKernel.Paging;

/// <summary>Standard paged-list response contract (FR-GRID-002), carried as the `Data` of a ResponseDto.</summary>
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
