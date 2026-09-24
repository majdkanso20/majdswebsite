using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

/// <summary>The audit log as CSV, Excel or PDF (newest first, capped at <see cref="CsvWriter.MaxRows"/>), honoring the same filters as the list.</summary>
[RequiresPermission(Permissions.Audit.View)]
public record ExportAuditLogQuery(AuditLogFilter Filter, string? Format) : IRequest<ExportFile>;

public class ExportAuditLogQueryHandler(ApplicationDbContext db) : IRequestHandler<ExportAuditLogQuery, ExportFile>
{
    public async Task<ExportFile> Handle(ExportAuditLogQuery request, CancellationToken ct)
    {
        var format = ExportFormats.Parse(request.Format);
        var query = AuditLogFilters.Apply(db.Set<AuditLogEntry>().AsNoTracking(), request.Filter);
        var rows = await query.OrderByDescending(a => a.CreatedAt).Take(CsvWriter.MaxRows).ToListAsync(ct);

        return TabularExport.Render(format, "Audit log", "audit-log", rows,
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
