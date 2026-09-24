using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

/// <summary>Users as CSV, Excel or PDF (capped at <see cref="CsvWriter.MaxRows"/>), narrowed by the same filters as the
/// list. Never includes password hashes or security tokens, only the fields the users list shows.</summary>
[RequiresPermission(Permissions.Users.View)]
public record ExportUsersQuery(string? Filter, bool? IsActive, string? Role, string? Format) : IRequest<ExportFile>;

public class ExportUsersQueryHandler(ApplicationDbContext db) : IRequestHandler<ExportUsersQuery, ExportFile>
{
    private record Row(string Id, string? Email, string? FullName, string? PhoneNumber, bool IsActive, DateTime CreatedAt);

    public async Task<ExportFile> Handle(ExportUsersQuery request, CancellationToken ct)
    {
        var format = ExportFormats.Parse(request.Format);
        var query = await UserQueryFilters.ApplyAsync(db, db.Users.AsNoTracking(), request.Filter, request.IsActive, request.Role, ct);

        var users = await query.OrderBy(u => u.Email).Take(CsvWriter.MaxRows)
            .Select(u => new Row(u.Id, u.Email, u.FullName, u.PhoneNumber, u.IsActive, u.CreatedAt)).ToListAsync(ct);

        var ids = users.Select(u => u.Id).ToList();
        var roleLookup = (await (from ur in db.UserRoles
                                 join r in db.Roles on ur.RoleId equals r.Id
                                 where ids.Contains(ur.UserId)
                                 select new { ur.UserId, r.Name }).ToListAsync(ct))
            .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(x => x.Name)));

        return TabularExport.Render(format, "Users", "users", users,
            ("Email", (Row u) => u.Email),
            ("Full name", u => u.FullName),
            ("Phone number", u => u.PhoneNumber),
            ("Roles", u => roleLookup.GetValueOrDefault(u.Id, string.Empty)),
            ("Active", u => u.IsActive ? "Yes" : "No"),
            ("Created (UTC)", u => u.CreatedAt));
    }
}
