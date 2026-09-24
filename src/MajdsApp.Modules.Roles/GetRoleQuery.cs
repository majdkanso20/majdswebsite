using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

[RequiresPermission(Permissions.Roles.View)]
public record GetRoleQuery(string RoleId) : IRequest<RoleDto>;

public class GetRoleQueryHandler(ApplicationDbContext db) : IRequestHandler<GetRoleQuery, RoleDto>
{
    public async Task<RoleDto> Handle(GetRoleQuery request, CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RoleId, ct)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        var userCount = await db.UserRoles.CountAsync(ur => ur.RoleId == role.Id, ct);

        return new RoleDto(role.Id, role.Name!, role.DisplayName, role.IsStatic, role.IsDefault, userCount);
    }
}
