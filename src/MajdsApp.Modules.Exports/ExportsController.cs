using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Exports;

[Authorize]
public class ExportsController(IMediator mediator) : ApiControllerBase
{
    /// <summary>Queues a background export and returns the job straight away; the user is notified when it is ready.</summary>
    [HttpPost("start")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<ResponseDto<ExportJobDto>> Start([FromBody] StartExportCommand command) =>
        Ok(await mediator.Send(command));

    /// <summary>The caller's recent exports with their status.</summary>
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<ExportJobDto>>> List() =>
        Ok(await mediator.Send(new ListMyExportsQuery()));

    /// <summary>The finished file (not the envelope).</summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] Guid jobId)
    {
        var file = await mediator.Send(new DownloadExportQuery(jobId));
        return File(file.Content, file.ContentType, file.FileName);
    }
}
