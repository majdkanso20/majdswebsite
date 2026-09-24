using MajdsApp.SharedKernel.Search;
using System.Linq.Expressions;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Audit;

/// <summary>The one place the viewer and the CSV export narrow the log, so they can never disagree (FR-AUDIT-003).</summary>
public static class AuditLogFilters
{
    public static IQueryable<AuditLogEntry> Apply(IQueryable<AuditLogEntry> query, AuditLogFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var pattern = LikePattern.Contains(filter.Text);
            query = query.Where(a => EF.Functions.Like(a.Action, pattern, LikePattern.Escape) || EF.Functions.Like(a.UserName!, pattern, LikePattern.Escape));
        }

        if (filter.From is { } from)
            query = query.Where(a => a.CreatedAt >= from.Date);

        if (filter.To is { } to)
        {
            var endExclusive = to.Date.AddDays(1);
            query = query.Where(a => a.CreatedAt < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Outcome))
        {
            var outcome = filter.Outcome.Trim();
            query = outcome.Equals("Success", StringComparison.OrdinalIgnoreCase) ? query.Where(a => a.Succeeded)
                : outcome.Equals("Failure", StringComparison.OrdinalIgnoreCase) ? query.Where(a => !a.Succeeded)
                : query.Where(a => a.Outcome == outcome);
        }

        return query;
    }
}

[RequiresPermission(Permissions.Audit.View)]
public record GetAuditLogQuery(PagedRequest Request, AuditLogFilter Filter) : IRequest<PagedResponse<AuditLogEntryDto>>;

public class GetAuditLogQueryHandler(ApplicationDbContext db) : IRequestHandler<GetAuditLogQuery, PagedResponse<AuditLogEntryDto>>
{
    public Task<PagedResponse<AuditLogEntryDto>> Handle(GetAuditLogQuery request, CancellationToken ct)
    {
        var query = AuditLogFilters.Apply(db.Set<AuditLogEntry>().AsNoTracking(), request.Filter with { Text = request.Request.Filter });

        var sortableColumns = new Dictionary<string, Expression<Func<AuditLogEntry, object>>>
        {
            ["action"] = a => a.Action,
            ["userName"] = a => a.UserName!,
            ["createdAt"] = a => a.CreatedAt,
            ["outcome"] = a => a.Outcome,
            ["durationMs"] = a => a.DurationMs
        };

        return query.ApplyPagingAsync(request.Request, sortableColumns,
            a => new AuditLogEntryDto(a.Id, a.Action, a.UserName, a.CreatedAt, a.Outcome, a.Succeeded, a.DurationMs, a.ClientIp, a.HttpMethod, a.Url), ct);
    }
}

/// <summary>One entry with its redacted parameters and property-level changes (GET /api/audit/get).</summary>
[RequiresPermission(Permissions.Audit.View)]
public record GetAuditLogEntryQuery(int Id) : IRequest<AuditLogEntryDetailDto>;

public class GetAuditLogEntryQueryHandler(ApplicationDbContext db) : IRequestHandler<GetAuditLogEntryQuery, AuditLogEntryDetailDto>
{
    public async Task<AuditLogEntryDetailDto> Handle(GetAuditLogEntryQuery request, CancellationToken ct)
    {
        var entry = await db.Set<AuditLogEntry>().AsNoTracking().AsSplitQuery()
            .Include(a => a.Changes).ThenInclude(c => c.Properties)
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException("Audit entry not found.");

        return new AuditLogEntryDetailDto(
            entry.Id, entry.Action, entry.UserId, entry.UserName, entry.CreatedAt,
            entry.Outcome, entry.Succeeded, entry.DurationMs, entry.ClientIp, entry.ClientBrowser,
            entry.HttpMethod, entry.Url, entry.Error, entry.Parameters,
            entry.Changes.Select(c => new AuditEntityChangeDto(c.EntityType, c.EntityId, c.ChangeType,
                c.Properties.Select(p => new AuditPropertyChangeDto(p.PropertyName, p.OriginalValue, p.NewValue)).ToList())).ToList());
    }
}
