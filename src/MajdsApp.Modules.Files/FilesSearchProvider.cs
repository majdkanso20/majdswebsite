using MajdsApp.Data;
using MajdsApp.SharedKernel.Search;
using MajdsApp.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Files;

/// <summary>Searches files by name, applying the same visibility rule as the Files list: everything for
/// Files.View holders, otherwise only the caller's own.</summary>
public class FilesSearchProvider(ApplicationDbContext db, ICurrentUser currentUser, IPermissionChecker permissions) : ISearchProvider
{
    public string Category => "Files";
    public string? RequiredPermission => null;
    public string? RequiredFeature => "Files";

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string term, int max, CancellationToken ct)
    {
        var pattern = LikePattern.Contains(term);
        var query = db.Set<FileRecord>().AsNoTracking().Where(f => EF.Functions.Like(f.FileName, pattern, LikePattern.Escape));
        if (!await permissions.HasPermissionAsync(Permissions.Files.View, ct))
        {
            var userId = currentUser.UserId;
            query = query.Where(f => f.OwnerId == userId);
        }

        return (await query.OrderByDescending(f => f.CreatedAt).Take(max).Select(f => new { f.FileName, f.OwnerName }).ToListAsync(ct))
            .Select(f => new SearchResult(Category, f.FileName, f.OwnerName, $"/files?q={Uri.EscapeDataString(f.FileName)}"))
            .ToList();
    }
}
