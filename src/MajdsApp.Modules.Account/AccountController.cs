using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Account;

[Authorize]
public class AccountController(IMediator mediator) : ApiControllerBase
{
    /// <summary>Whether the login page should offer "Create account" (the self-registration setting).</summary>
    [AllowAnonymous]
    [HttpGet("registration-open")]
    public async Task<ResponseDto<bool>> RegistrationOpen([FromServices] ISettingsProvider settings) =>
        Ok(await settings.GetBooleanAsync("Security.AllowSelfRegistration"));

    [AllowAnonymous]
    [HttpPost("register")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Auth)]
    public async Task<ResponseDto<object?>> Register([FromBody] RegisterCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    /// <summary>Requests a reset link by email. Always succeeds so the response can't be used to enumerate accounts.</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Auth)]
    public async Task<ResponseDto<object?>> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    /// <summary>Consumes the link from <see cref="ForgotPassword"/> to set a new password.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Auth)]
    public async Task<ResponseDto<object?>> ResetPassword([FromBody] ResetMyPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpGet("me")]
    public async Task<ResponseDto<ProfileDto>> Me() => Ok(await mediator.Send(new GetMyProfileQuery()));

    [HttpPost("update-profile")]
    public async Task<ResponseDto<object?>> UpdateProfile([FromBody] UpdateMyProfileCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("change-password")]
    public async Task<ResponseDto<object?>> ChangePassword([FromBody] ChangeMyPasswordCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("picture")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ResponseDto<object?>> SetPicture(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        await mediator.Send(new SetMyPictureCommand(file.ContentType, file.Length, stream));
        return Ok<object?>(null);
    }

    /// <summary>Raw image bytes (not the envelope), like F-Files' download.</summary>
    [HttpGet("picture")]
    public async Task<IActionResult> GetPicture()
    {
        var picture = await mediator.Send(new GetMyPictureQuery());
        return File(picture.Content, picture.ContentType);
    }
}
