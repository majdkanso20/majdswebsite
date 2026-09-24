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

public class GetUserQueryHandler(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new NotFoundException($"User '{request.UserId}' was not found.");

        var roles = await userManager.GetRolesAsync(user);
        var lockedOut = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;

        return new UserDto(user.Id, user.Email!, user.FullName, user.PhoneNumber, user.IsActive,
            user.EmailConfirmed, lockedOut, user.TwoFactorEnabled, roles.ToList(), user.CreatedAt);
    }
}
