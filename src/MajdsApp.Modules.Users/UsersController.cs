using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Users;

[Authorize]
public class UsersController(IMediator mediator) : ApiControllerBase
{
    /// <summary>A file (csv, xlsx or pdf; not the envelope), like F-Files' download. Takes the list's filters.</summary>
    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<IActionResult> Export(
        [FromQuery] string? filter, [FromQuery] bool? isActive, [FromQuery] string? role, [FromQuery] string? format)
    {
        var file = await mediator.Send(new ExportUsersQuery(filter, isActive, role, format));
        return File(file.Content, file.ContentType, file.FileName);
    }

    // Importing users runs as a background job through the generic Imports module (F-Export FR-EXP-004):
    // POST /api/imports/start?source=users and GET /api/imports/template?source=users (UsersImportSource, ImportUsers.cs).

    [HttpPost("reset-two-factor")]
    public async Task<ResponseDto<object?>> ResetTwoFactor([FromBody] ResetTwoFactorCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<UserDto>>> List(
        [FromQuery] PagedRequest request, [FromQuery] bool? isActive, [FromQuery] string? role) =>
        Ok(await mediator.Send(new ListUsersQuery(request, isActive, role)));

    [HttpGet("get")]
    public async Task<ResponseDto<UserDto>> Get([FromQuery] string userId) =>
        Ok(await mediator.Send(new GetUserQuery(userId)));

    [HttpPost("create")]
    public async Task<ResponseDto<string>> Create([FromBody] CreateUserCommand command) =>
        Ok(await mediator.Send(command));

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateUserCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("set-activation")]
    public async Task<ResponseDto<object?>> SetActivation([FromBody] SetUserActivationCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteUserCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("reset-password")]
    public async Task<ResponseDto<object?>> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("unlock")]
    public async Task<ResponseDto<object?>> Unlock([FromBody] UnlockUserCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
