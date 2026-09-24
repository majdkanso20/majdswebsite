using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Features;

[Authorize]
public class FeaturesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<FeatureDto>>> List() => Ok(await mediator.Send(new ListFeaturesQuery()));

    [HttpGet("enabled")]
    public async Task<ResponseDto<IReadOnlyCollection<string>>> Enabled() => Ok(await mediator.Send(new GetEnabledFeaturesQuery()));

    [HttpPost("set")]
    public async Task<ResponseDto<object?>> Set([FromBody] SetFeatureCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
