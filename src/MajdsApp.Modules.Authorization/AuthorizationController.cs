using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Authorization;

[Authorize]
public class PermissionsController : ApiControllerBase
{
    /// <summary>Full permission tree for the role editor (FR-AUTHZ-002).</summary>
    [HttpGet("tree")]
    public ResponseDto<IReadOnlyList<PermissionGroup>> Tree() => Ok(PermissionRegistry.GetAllGroups());
}

[Authorize]
[Route("api/session")]
public class SessionController(PermissionChecker permissionChecker) : ApiControllerBase
{
    /// <summary>Current user's effective permissions, for the frontend to drive UI visibility (FR-AUTHZ-005).</summary>
    [HttpGet("permissions")]
    public async Task<ResponseDto<IReadOnlyList<string>>> Permissions() =>
        Ok(await permissionChecker.GetEffectivePermissionsAsync());
}

[Authorize]
[Route("api/roles/permissions")]
public class RolePermissionsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("get")]
    public async Task<ResponseDto<IReadOnlyList<string>>> Get([FromQuery] string roleId) =>
        Ok(await mediator.Send(new GetRolePermissionsQuery(roleId)));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateRolePermissionsCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
