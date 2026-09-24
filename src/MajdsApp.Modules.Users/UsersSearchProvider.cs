using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Search;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

public class UsersSearchProvider(ApplicationDbContext db) : ISearchProvider
{
    public string Category => "Users";
    public string? RequiredPermission => Permissions.Users.View;
    public string? RequiredFeature => null;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string term, int max, CancellationToken ct)
    {
        var pattern = LikePattern.Contains(term);
        return (await db.Users.AsNoTracking()
            .Where(u => EF.Functions.Like(u.Email!, pattern, LikePattern.Escape) || EF.Functions.Like(u.FullName!, pattern, LikePattern.Escape))
            .OrderBy(u => u.Email).Take(max).Select(u => new { u.Email, u.FullName }).ToListAsync(ct))
        .Select(u => new SearchResult(Category, u.Email ?? "(no email)", u.FullName,
            $"/administration/users?q={Uri.EscapeDataString(u.Email ?? string.Empty)}"))
        .ToList();
    }
}
