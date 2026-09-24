using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

/// <summary>CSV of the audit log (newest first, capped at <see cref="CsvWriter.MaxRows"/>), honoring the same filters as the list.</summary>
[RequiresPermission(Permissions.Audit.View)]
public record ExportAuditLogQuery(AuditLogFilter Filter) : IRequest<byte[]>;

public class ExportAuditLogQueryHandler(ApplicationDbContext db) : IRequestHandler<ExportAuditLogQuery, byte[]>
{
    public async Task<byte[]> Handle(ExportAuditLogQuery request, CancellationToken ct)
    {
        var query = AuditLogFilters.Apply(db.Set<AuditLogEntry>().AsNoTracking(), request.Filter);
        var rows = await query.OrderByDescending(a => a.CreatedAt).Take(CsvWriter.MaxRows).ToListAsync(ct);

        return CsvWriter.Write(rows,
            ("When (UTC)", (AuditLogEntry a) => a.CreatedAt),
            ("Action", a => a.Action),
            ("User", a => a.UserName),
            ("Outcome", a => a.Outcome),
            ("Duration (ms)", a => a.DurationMs),
            ("Method", a => a.HttpMethod),
            ("Path", a => a.Url),
            ("Client IP", a => a.ClientIp),
            ("Error", a => a.Error));
    }
}
