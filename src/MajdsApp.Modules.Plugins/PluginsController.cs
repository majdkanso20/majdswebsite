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
