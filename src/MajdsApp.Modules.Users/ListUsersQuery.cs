using MajdsApp.SharedKernel.Search;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

/// <summary>FR-USER-001. <see cref="PagedRequest.Filter"/> free-text searches name/email; <see cref="IsActive"/>
/// and <see cref="Role"/> are the extra "optional column filters" F-Data's contract allows.</summary>
[RequiresPermission(Permissions.Users.View)]
public record ListUsersQuery(PagedRequest Request, bool? IsActive, string? Role) : IRequest<PagedResponse<UserDto>>;

public class ListUsersQueryHandler(ApplicationDbContext db) : IRequestHandler<ListUsersQuery, PagedResponse<UserDto>>
{
    public async Task<PagedResponse<UserDto>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(u => EF.Functions.Like(u.Email!, pattern, LikePattern.Escape) || EF.Functions.Like(u.FullName!, pattern, LikePattern.Escape));
        }

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleId = await db.Roles.Where(r => r.Name == request.Role).Select(r => r.Id).FirstOrDefaultAsync(ct);
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<ApplicationUser, object>>>
        {
            ["email"] = u => u.Email!,
            ["fullName"] = u => u.FullName!,
            ["createdAt"] = u => u.CreatedAt
        };

        var page = await query.ApplyPagingAsync(request.Request, sortableColumns, u => new
        {
            u.Id,
            Email = u.Email!,
            u.FullName,
            u.PhoneNumber,
            u.IsActive,
            u.EmailConfirmed,
            LockedOut = u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow,
            u.TwoFactorEnabled,
            u.CreatedAt
        }, ct);

        var userIds = page.Items.Select(u => u.Id).ToList();
        var rolesByUser = await db.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleName = r.Name! })
            .ToListAsync(ct);

        var items = page.Items.Select(u => new UserDto(
            u.Id, u.Email, u.FullName, u.PhoneNumber, u.IsActive, u.EmailConfirmed, u.LockedOut, u.TwoFactorEnabled,
            rolesByUser.Where(r => r.UserId == u.Id).Select(r => r.RoleName).ToList(),
            u.CreatedAt)).ToList();

        return new PagedResponse<UserDto>
        {
            Items = items,
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }
}
