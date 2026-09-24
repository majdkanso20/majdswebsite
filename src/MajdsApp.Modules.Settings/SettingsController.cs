using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Settings;

[Authorize]
public class SettingsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<SettingDto>>> List() =>
        Ok(await mediator.Send(new GetSettingsQuery()));

    /// <summary>Client-visible settings (e.g. application name) for the shell — any signed-in user.</summary>
    [HttpGet("public")]
    public async Task<ResponseDto<IReadOnlyDictionary<string, string>>> Public() =>
        Ok(await mediator.Send(new GetPublicSettingsQuery()));

    /// <summary>The caller's own overridable settings (FR-SET-003, User scope).</summary>
    [HttpGet("my")]
    public async Task<ResponseDto<IReadOnlyList<SettingDto>>> My() =>
        Ok(await mediator.Send(new GetMySettingsQuery()));

    [HttpPost("update-mine")]
    public async Task<ResponseDto<object?>> UpdateMine([FromBody] UpdateMySettingsCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("update")]
    public async Task<ResponseDto<object?>> Update([FromBody] UpdateSettingsCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
