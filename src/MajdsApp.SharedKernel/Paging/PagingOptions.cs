using System.ComponentModel.DataAnnotations;

namespace MajdsApp.SharedKernel.Paging;

/// <summary>The <c>Paging</c> section (F-Data FR-GRID-004): how large a page a list endpoint will return, so one request cannot ask for the whole table.</summary>
public class PagingOptions
{
    [Range(1, 1000, ErrorMessage = "Paging:MaxPageSize must be between 1 and 1000.")]
    public int MaxPageSize { get; set; } = 100;
}
