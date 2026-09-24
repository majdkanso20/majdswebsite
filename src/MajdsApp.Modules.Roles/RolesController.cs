using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Roles;

[Authorize]
public class RolesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<RoleDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListRolesQuery(request)));

    [HttpGet("get")]
    public async Task<ResponseDto<RoleDto>> Get([FromQuery] string roleId) =>
        Ok(await mediator.Send(new GetRoleQuery(roleId)));

    [HttpPost("create")]
    public async Task<ResponseDto<string>> Create([FromBody] CreateRoleCommand command) =>
        Ok(await mediator.Send(command));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateRoleCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteRoleCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
