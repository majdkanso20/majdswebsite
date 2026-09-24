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

    [HttpPost("run")]
    public async Task<ResponseDto<bool>> Run([FromBody] RunJobCommand command) => Ok(await mediator.Send(command));
}
