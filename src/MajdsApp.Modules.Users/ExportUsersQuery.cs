using MajdsApp.SharedKernel.Search;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

/// <summary>CSV of users (capped at <see cref="CsvWriter.MaxRows"/>). Never includes password hashes or
/// security tokens — only the same fields the users list shows.</summary>
[RequiresPermission(Permissions.Users.View)]
public record ExportUsersQuery(string? Filter) : IRequest<byte[]>;

public class ExportUsersQueryHandler(ApplicationDbContext db) : IRequestHandler<ExportUsersQuery, byte[]>
{
    private record Row(string Id, string? Email, string? FullName, string? PhoneNumber, bool IsActive, DateTime CreatedAt);

    public async Task<byte[]> Handle(ExportUsersQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Filter))
        {
            var pattern = LikePattern.Contains(request.Filter);
            query = query.Where(u => EF.Functions.Like(u.Email!, pattern, LikePattern.Escape) || EF.Functions.Like(u.FullName!, pattern, LikePattern.Escape));
        }

        var users = await query.OrderBy(u => u.Email).Take(CsvWriter.MaxRows)
            .Select(u => new Row(u.Id, u.Email, u.FullName, u.PhoneNumber, u.IsActive, u.CreatedAt)).ToListAsync(ct);

        var ids = users.Select(u => u.Id).ToList();
        var roleLookup = (await (from ur in db.UserRoles
                                 join r in db.Roles on ur.RoleId equals r.Id
                                 where ids.Contains(ur.UserId)
                                 select new { ur.UserId, r.Name }).ToListAsync(ct))
            .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(x => x.Name)));

        return CsvWriter.Write(users,
            ("Email", (Row u) => u.Email),
            ("Full name", u => u.FullName),
            ("Phone number", u => u.PhoneNumber),
            ("Roles", u => roleLookup.GetValueOrDefault(u.Id, string.Empty)),
            ("Active", u => u.IsActive ? "Yes" : "No"),
            ("Created (UTC)", u => u.CreatedAt));
    }
}
