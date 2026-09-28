using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Mapping;
using MajdsApp.Data;
using MajdsApp.Modules.Authorization;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Users;

[RequiresPermission(Permissions.Users.View)]
public record GetUserQuery(string UserId) : IRequest<UserDto>;

public class GetUserQueryHandler(IReadRepository<ApplicationUser, string> users, UserManager<ApplicationUser> userManager, IObjectMapper mapper) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        return mapper.Map<UserDto>(user) with { Roles = (await userManager.GetRolesAsync(user)).ToList() };
    }
}
