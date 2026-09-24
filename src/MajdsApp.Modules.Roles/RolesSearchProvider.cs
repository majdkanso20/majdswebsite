using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Search;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

public class RolesSearchProvider(ApplicationDbContext db) : ISearchProvider
{
    public string Category => "Roles";
    public string? RequiredPermission => Permissions.Roles.View;
    public string? RequiredFeature => null;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string term, int max, CancellationToken ct)
    {
        var pattern = LikePattern.Contains(term);
        return (await db.Roles.AsNoTracking()
            .Where(r => EF.Functions.Like(r.Name!, pattern, LikePattern.Escape) || EF.Functions.Like(r.DisplayName!, pattern, LikePattern.Escape))
            .OrderBy(r => r.Name).Take(max).Select(r => new { r.Name, r.DisplayName }).ToListAsync(ct))
        .Select(r => new SearchResult(Category, r.Name ?? string.Empty, r.DisplayName,
            $"/administration/roles?q={Uri.EscapeDataString(r.Name ?? string.Empty)}"))
        .ToList();
    }
}
