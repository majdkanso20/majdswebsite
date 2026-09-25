using System.Globalization;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Export;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

/// <summary>The audit log as CSV, Excel or PDF (newest first, capped at <see cref="CsvWriter.MaxRows"/>), honoring the same filters as the list.</summary>
[RequiresPermission(Permissions.Audit.View)]
public record ExportAuditLogQuery(AuditLogFilter Filter, string? Format) : IRequest<ExportFile>;

public class ExportAuditLogQueryHandler(AuditExportSource source) : IRequestHandler<ExportAuditLogQuery, ExportFile>
{
    public Task<ExportFile> Handle(ExportAuditLogQuery request, CancellationToken ct)
    {
        var filters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(request.Filter.Text)) filters["filter"] = request.Filter.Text;
        if (request.Filter.From is { } from) filters["from"] = from.ToString("O", CultureInfo.InvariantCulture);
        if (request.Filter.To is { } to) filters["to"] = to.ToString("O", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(request.Filter.Outcome)) filters["outcome"] = request.Filter.Outcome;

        return source.BuildAsync(ExportFormats.Parse(request.Format), filters, CsvWriter.MaxRows, ct);
    }
}

/// <summary>The audit log dataset for F-Export, used by the immediate download and by background exports.</summary>
public class AuditExportSource(ApplicationDbContext db) : IExportSource
{
    public string Key => "audit";
    public string Title => "Audit log";
    public string? Permission => Permissions.Audit.View;

    public async Task<ExportFile> BuildAsync(ExportFormat format, IReadOnlyDictionary<string, string> filters, int maxRows, CancellationToken ct)
    {
        var filter = new AuditLogFilter(
            filters.GetValueOrDefault("filter"), ParseDate(filters.GetValueOrDefault("from")), ParseDate(filters.GetValueOrDefault("to")),
            filters.GetValueOrDefault("outcome"));

        var query = AuditLogFilters.Apply(db.Set<AuditLogEntry>().AsNoTracking(), filter);
        var rows = await query.OrderByDescending(a => a.CreatedAt).Take(maxRows).ToListAsync(ct);

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

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null;
}
