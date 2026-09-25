using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Jobs;

[Authorize]
public class JobsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<JobDto>>> List() => Ok(await mediator.Send(new ListJobsQuery()));

    [HttpGet("runs")]
    public async Task<ResponseDto<PagedResponse<JobRunDto>>> Runs([FromQuery] PagedRequest request, [FromQuery] string? jobName) =>
        Ok(await mediator.Send(new ListJobRunsQuery(request, jobName)));

    /// <summary>The queued one-off jobs with their status and attempts (FR-JOB-003).</summary>
    [HttpGet("queue")]
    public async Task<ResponseDto<PagedResponse<BackgroundJobDto>>> Queue([FromQuery] PagedRequest request, [FromQuery] string? status) =>
        Ok(await mediator.Send(new ListBackgroundJobsQuery(request, status)));

    [HttpPost("retry")]
    public async Task<ResponseDto<object?>> Retry([FromBody] RetryBackgroundJobCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteBackgroundJobCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }

    [HttpPost("run")]
    public async Task<ResponseDto<bool>> Run([FromBody] RunJobCommand command) => Ok(await mediator.Send(command));
}
