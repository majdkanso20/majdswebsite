using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Mapping;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Roles;

[RequiresPermission(Permissions.Roles.View)]
public record GetRoleQuery(string RoleId) : IRequest<RoleDto>;

public class GetRoleQueryHandler(IReadRepository<ApplicationRole, string> roles, ApplicationDbContext db, IObjectMapper mapper) : IRequestHandler<GetRoleQuery, RoleDto>
{
    public async Task<RoleDto> Handle(GetRoleQuery request, CancellationToken ct)
    {
        var role = await roles.GetByIdAsync(request.RoleId, ct)
            ?? throw new NotFoundException($"Role '{request.RoleId}' was not found.");

        var userCount = await db.UserRoles.CountAsync(ur => ur.RoleId == role.Id, ct);

        return mapper.Map<RoleDto>(role) with { UserCount = userCount };
    }
}
