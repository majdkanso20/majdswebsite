using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Audit;

/// <summary>Read-only by design: there is deliberately no endpoint that changes or deletes audit records (FR-AUDIT-005).</summary>
[Authorize]
public class AuditController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<AuditLogEntryDto>>> List(
        [FromQuery] PagedRequest request, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome) =>
        Ok(await mediator.Send(new GetAuditLogQuery(request, new AuditLogFilter(null, from, to, outcome))));

    [HttpGet("get")]
    public async Task<ResponseDto<AuditLogEntryDetailDto>> Get([FromQuery] int id) =>
        Ok(await mediator.Send(new GetAuditLogEntryQuery(id)));

    /// <summary>CSV file (not the envelope), like F-Files' download.</summary>
    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<IActionResult> Export(
        [FromQuery] string? filter, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome) =>
        File(await mediator.Send(new ExportAuditLogQuery(new AuditLogFilter(filter, from, to, outcome))), "text/csv", "audit-log.csv");
}
