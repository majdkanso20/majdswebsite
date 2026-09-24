using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Diagnostics;

/// <summary>
/// Smoke-test module (not part of the domain SRS) proving the Phase 0 backbone end-to-end:
/// GET/POST-only + ResponseDto (P1), a separate self-registering project (P2), the generic
/// repository/UoW against a real owned entity (P3), MediatR pipeline behaviors (P4), paging
/// (F-Data), and centralized exception handling (F-Errors).
/// </summary>
public class DiagnosticsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("ping")]
    public async Task<ResponseDto<string>> Ping() => Ok(await mediator.Send(new PingQuery()));

    [HttpPost("ping/record")]
    public async Task<ResponseDto<Guid>> RecordPing([FromBody] RecordPingCommand command) =>
        Ok(await mediator.Send(command));

    [HttpGet("ping/list")]
    public async Task<ResponseDto<PagedResponse<PingListItem>>> ListPings([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListPingsQuery(request)));

    [HttpGet("boom")]
    public async Task<ResponseDto<string>> Boom() => Ok(await mediator.Send(new BoomQuery()));
}
