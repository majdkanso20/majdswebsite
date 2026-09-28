using MajdsApp.SharedKernel.Search;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Mapping;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

[RequiresPermission(Permissions.Roles.View)]
public record ListRolesQuery(PagedRequest Request) : IRequest<PagedResponse<RoleDto>>;

public class ListRolesQueryHandler(IReadRepository<ApplicationRole, string> roles, ApplicationDbContext db, IObjectMapper mapper) : IRequestHandler<ListRolesQuery, PagedResponse<RoleDto>>
{
    public async Task<PagedResponse<RoleDto>> Handle(ListRolesQuery request, CancellationToken ct)
    {
        var query = roles.Query();

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(r => EF.Functions.Like(r.Name!, pattern, LikePattern.Escape) || EF.Functions.Like(r.DisplayName!, pattern, LikePattern.Escape));
        }

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<ApplicationRole, object>>>
        {
            ["name"] = r => r.Name!,
            ["displayName"] = r => r.DisplayName!
        };

        var page = await query.ApplyPagingAsync(request.Request, sortableColumns, mapper.Projection<ApplicationRole, RoleDto>(), ct);

        // How many users hold each role on this page, in one query. UserRoles is a join table with no single-key
        // entity of its own (composite key), so it is read through the context directly rather than a repository.
        var ids = page.Items.Select(r => r.Id).ToList();
        var counts = await db.UserRoles.Where(ur => ids.Contains(ur.RoleId)).GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.RoleId, g => g.Count, ct);

        return new PagedResponse<RoleDto>
        {
            Items = page.Items.Select(r => r with { UserCount = counts.GetValueOrDefault(r.Id) }).ToList(),
            TotalCount = page.TotalCount, Page = page.Page, PageSize = page.PageSize
        };
    }
}
