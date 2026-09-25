using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Plugins;

[Authorize]
public class PluginsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<PluginDto>>> List() => Ok(await mediator.Send(new ListPluginsQuery()));

    /// <summary>Verifies an uploaded plugin package (.zip) and stages it for the next start. Optional form field
    /// <c>sha256</c> is the publisher's checksum, which must match.</summary>
    [HttpPost("install")]
    [RequestSizeLimit(60L * 1024 * 1024)]
    public async Task<ResponseDto<PluginChangeDto>> Install(Microsoft.AspNetCore.Http.IFormFile file, [FromForm] string? sha256)
    {
        if (file is null || file.Length == 0)
            throw new FluentValidation.ValidationException("Choose a plugin package (.zip) to install.");

        await using var stream = file.OpenReadStream();
        return Ok(await mediator.Send(new InstallPluginCommand(stream, file.FileName, sha256)));
    }

    [HttpPost("uninstall")]
    public async Task<ResponseDto<object?>> Uninstall([FromBody] UninstallPluginCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("rollback")]
    public async Task<ResponseDto<PluginChangeDto>> Rollback([FromBody] RollbackPluginCommand command) =>
        Ok(await mediator.Send(command));

    [HttpPost("cancel-pending")]
    public async Task<ResponseDto<object?>> CancelPending([FromBody] CancelPluginChangeCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    /// <summary>Installs, upgrades, rollbacks and uninstalls that will be applied at the next start.</summary>
    [HttpGet("pending")]
    public async Task<ResponseDto<IReadOnlyList<PendingPluginChangeDto>>> Pending() =>
        Ok(await mediator.Send(new ListPendingPluginChangesQuery()));

    [HttpPost("set-enabled")]
    public async Task<ResponseDto<object?>> SetEnabled([FromBody] SetPluginEnabledCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpGet("manifest")]
    public async Task<ResponseDto<IReadOnlyList<PluginMenuEntryDto>>> Manifest() =>
        Ok(await mediator.Send(new GetPluginMenuQuery()));
}
