using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Users;

[Authorize]
public class UsersController(IMediator mediator) : ApiControllerBase
{
    /// <summary>CSV file (not the envelope), like F-Files' download.</summary>
    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<IActionResult> Export([FromQuery] string? filter) =>
        File(await mediator.Send(new ExportUsersQuery(filter)), "text/csv", "users.csv");

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
