using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Authorization;

/// <summary>
/// One user's permissions, explained (F-Authorization FR-AUTHZ-003): what their roles give them, the grants and denies set directly on the user, and the result.
/// A direct grant adds a permission the roles do not give; a direct deny removes one they do. A super administrator holds everything regardless.
/// </summary>
public record UserPermissionsDto(
    string UserId, bool IsSuperAdmin, IReadOnlyList<string> FromRoles, IReadOnlyList<string> Granted, IReadOnlyList<string> Denied, IReadOnlyList<string> Effective);

[RequiresPermission(Permissions.Users.View)]
public record GetUserPermissionsQuery(string UserId) : IRequest<UserPermissionsDto>;

public class GetUserPermissionsQueryHandler(ApplicationDbContext db, PermissionChecker checker)
    : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(GetUserPermissionsQuery request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        var roleIds = await db.UserRoles.Where(ur => ur.UserId == request.UserId).Select(ur => ur.RoleId).ToListAsync(ct);
        var isSuperAdmin = await db.Roles.AnyAsync(r => roleIds.Contains(r.Id) && r.Name == PermissionChecker.SuperAdminRoleName, ct);
        var fromRoles = await db.Set<RolePermission>().Where(rp => roleIds.Contains(rp.RoleId) && rp.IsGranted).Select(rp => rp.PermissionName).Distinct().OrderBy(n => n).ToListAsync(ct);
        var overrides = await db.Set<UserPermission>().Where(up => up.UserId == request.UserId).ToListAsync(ct);
        var effective = await checker.GetEffectivePermissionsForAsync(request.UserId, ct);

        return new UserPermissionsDto(request.UserId, isSuperAdmin, fromRoles,
            overrides.Where(o => o.IsGranted).Select(o => o.PermissionName).OrderBy(n => n).ToList(),
            overrides.Where(o => !o.IsGranted).Select(o => o.PermissionName).OrderBy(n => n).ToList(),
            effective.OrderBy(n => n).ToList());
    }
}

/// <summary>Replaces the grants and denies set directly on a user. Takes effect at once (their cached permissions are dropped).</summary>
[RequiresPermission(Permissions.Users.Edit)]
public record UpdateUserPermissionsCommand(string UserId, IReadOnlyList<string> Granted, IReadOnlyList<string> Denied)
    : IRequest, ITransactionalCommand, IAuditableCommand;

public class UpdateUserPermissionsCommandValidator : AbstractValidator<UpdateUserPermissionsCommand>
{
    public UpdateUserPermissionsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x).Custom((command, context) =>
        {
            var known = new HashSet<string>(PermissionRegistry.GetAllPermissionNames());
            foreach (var name in command.Granted.Concat(command.Denied).Distinct().Where(n => !known.Contains(n)))
                context.AddFailure($"'{name}' is not a permission.");

            foreach (var name in command.Granted.Intersect(command.Denied))
                context.AddFailure($"'{name}' cannot be both granted and denied.");
        });
    }
}

public class UpdateUserPermissionsCommandHandler(ApplicationDbContext db, PermissionChecker checker)
    : IRequestHandler<UpdateUserPermissionsCommand>
{
    public async Task Handle(UpdateUserPermissionsCommand request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        db.Set<UserPermission>().RemoveRange(await db.Set<UserPermission>().Where(up => up.UserId == request.UserId).ToListAsync(ct));
        foreach (var name in request.Granted.Distinct())
            db.Set<UserPermission>().Add(new UserPermission { UserId = request.UserId, PermissionName = name, IsGranted = true });
        foreach (var name in request.Denied.Distinct())
            db.Set<UserPermission>().Add(new UserPermission { UserId = request.UserId, PermissionName = name, IsGranted = false });

        checker.InvalidateUser(request.UserId);
    }
}

[Authorize]
[Route("api/users/permissions")]
public class UserPermissionsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("get")]
    public async Task<ResponseDto<UserPermissionsDto>> Get([FromQuery] string userId) => Ok(await mediator.Send(new GetUserPermissionsQuery(userId)));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateUserPermissionsCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
