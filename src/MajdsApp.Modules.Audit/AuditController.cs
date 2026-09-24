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

    /// <summary>A file (csv, xlsx or pdf; not the envelope), like F-Files' download. Takes the list's filters.</summary>
    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<IActionResult> Export(
        [FromQuery] string? filter, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? outcome, [FromQuery] string? format)
    {
        var file = await mediator.Send(new ExportAuditLogQuery(new AuditLogFilter(filter, from, to, outcome), format));
        return File(file.Content, file.ContentType, file.FileName);
    }
}
