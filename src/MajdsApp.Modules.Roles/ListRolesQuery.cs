using MajdsApp.SharedKernel.Search;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

[RequiresPermission(Permissions.Roles.View)]
public record ListRolesQuery(PagedRequest Request) : IRequest<PagedResponse<RoleDto>>;

public class ListRolesQueryHandler(ApplicationDbContext db) : IRequestHandler<ListRolesQuery, PagedResponse<RoleDto>>
{
    public Task<PagedResponse<RoleDto>> Handle(ListRolesQuery request, CancellationToken ct)
    {
        var query = db.Roles.AsNoTracking();

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

        return query.ApplyPagingAsync(request.Request, sortableColumns, r => new RoleDto(
            r.Id, r.Name!, r.DisplayName, r.IsStatic, r.IsDefault,
            db.UserRoles.Count(ur => ur.RoleId == r.Id)), ct);
    }
}
