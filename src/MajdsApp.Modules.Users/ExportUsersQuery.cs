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

public class ExportUsersQueryHandler(UsersExportSource source) : IRequestHandler<ExportUsersQuery, ExportFile>
{
    public Task<ExportFile> Handle(ExportUsersQuery request, CancellationToken ct)
    {
        var filters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(request.Filter)) filters["filter"] = request.Filter;
        if (request.IsActive.HasValue) filters["isActive"] = request.IsActive.Value.ToString();
        if (!string.IsNullOrWhiteSpace(request.Role)) filters["role"] = request.Role;

        return source.BuildAsync(ExportFormats.Parse(request.Format), filters, CsvWriter.MaxRows, ct);
    }
}

/// <summary>The users dataset for F-Export, used by the immediate download and by background exports.</summary>
public class UsersExportSource(ApplicationDbContext db) : IExportSource
{
    private record Row(string Id, string? Email, string? FullName, string? PhoneNumber, bool IsActive, DateTime CreatedAt);

    public string Key => "users";
    public string Title => "Users";
    public string? Permission => Permissions.Users.View;

    public async Task<ExportFile> BuildAsync(ExportFormat format, IReadOnlyDictionary<string, string> filters, int maxRows, CancellationToken ct)
    {
        bool? isActive = filters.TryGetValue("isActive", out var active) && bool.TryParse(active, out var parsed) ? parsed : null;
        var query = await UserQueryFilters.ApplyAsync(db, db.Users.AsNoTracking(),
            filters.GetValueOrDefault("filter"), isActive, filters.GetValueOrDefault("role"), ct);

        var users = await query.OrderBy(u => u.Email).Take(maxRows)
            .Select(u => new Row(u.Id, u.Email, u.FullName, u.PhoneNumber, u.IsActive, u.CreatedAt)).ToListAsync(ct);

        var ids = users.Select(u => u.Id).ToList();
        var roleLookup = new Dictionary<string, string>();
        // Batched so a very large export does not build one enormous IN (...) clause.
        foreach (var batch in ids.Chunk(500))
        {
            var pairs = await (from ur in db.UserRoles
                               join r in db.Roles on ur.RoleId equals r.Id
                               where batch.Contains(ur.UserId)
                               select new { ur.UserId, r.Name }).ToListAsync(ct);
            foreach (var group in pairs.GroupBy(x => x.UserId))
                roleLookup[group.Key] = string.Join("; ", group.Select(x => x.Name));
        }

        return TabularExport.Render(format, "Users", "users", users,
            ("Email", (Row u) => u.Email),
            ("Full name", u => u.FullName),
            ("Phone number", u => u.PhoneNumber),
            ("Roles", u => roleLookup.GetValueOrDefault(u.Id, string.Empty)),
            ("Active", u => u.IsActive ? "Yes" : "No"),
            ("Created (UTC)", u => u.CreatedAt));
    }
}
