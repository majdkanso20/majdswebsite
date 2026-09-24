using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Dashboard;

[Authorize]
public class DashboardController(IMediator mediator) : ApiControllerBase
{
    /// <summary>The widgets this user may see, in their layout order (FR-DASH-001/002/004).</summary>
    [HttpGet("widgets")]
    public async Task<ResponseDto<IReadOnlyList<DashboardWidgetDto>>> Widgets() =>
        Ok(await mediator.Send(new GetDashboardWidgetsQuery()));

    /// <summary>One widget's data; each tile calls this on its own, so a slow widget never blocks the others.</summary>
    [HttpGet("data")]
    public async Task<ResponseDto<object>> Data([FromQuery] string key) =>
        Ok(await mediator.Send(new GetDashboardWidgetDataQuery(key)));
}
