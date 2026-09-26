using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Authorization;

[Authorize]
public class PermissionsController(MajdsApp.SharedKernel.Plugins.IPluginStateCache pluginState) : ApiControllerBase
{
    /// <summary>Full permission tree for the role editor (FR-AUTHZ-002). A disabled plugin's permissions are left out, since nothing they protect is reachable
    /// while it is off (FR-AUTHZ-008); grants already made are kept and reappear when it is enabled.</summary>
    [HttpGet("tree")]
    public ResponseDto<IReadOnlyList<PermissionGroup>> Tree()
    {
        var hidden = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } name && name.StartsWith("MajdsApp.Plugins.", StringComparison.Ordinal) && !pluginState.IsEnabled(name))
            .SelectMany(PermissionRegistry.GetPermissionNames)
            .ToHashSet();

        return Ok<IReadOnlyList<PermissionGroup>>(PermissionRegistry.GetAllGroups()
            .Where(g => !g.Permissions.All(hidden.Contains))
            .ToList());
    }
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
