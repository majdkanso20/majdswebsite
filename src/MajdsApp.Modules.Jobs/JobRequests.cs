using System.Linq.Expressions;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Jobs;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Jobs;

public record JobDto(string Name, int IntervalMinutes, DateTime? LastRunAt, bool? LastSuccess, DateTime? NextRunAt);

public record JobRunDto(int Id, string JobName, DateTime StartedAt, int DurationMs, bool Success, string? Error, string Trigger);

[RequiresPermission(Permissions.Jobs.View)]
public record ListJobsQuery : IRequest<IReadOnlyList<JobDto>>;

public class ListJobsQueryHandler(ApplicationDbContext db, IEnumerable<IRecurringJob> jobs)
    : IRequestHandler<ListJobsQuery, IReadOnlyList<JobDto>>
{
    public async Task<IReadOnlyList<JobDto>> Handle(ListJobsQuery request, CancellationToken ct)
    {
        var result = new List<JobDto>();
        foreach (var job in jobs.OrderBy(j => j.Name))
        {
            var last = await db.Set<JobRun>().AsNoTracking().Where(r => r.JobName == job.Name)
                .OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);

            result.Add(new JobDto(job.Name, (int)job.Interval.TotalMinutes, last?.StartedAt, last?.Success,
                last is null ? null : last.StartedAt + job.Interval));
        }

        return result;
    }
}

[RequiresPermission(Permissions.Jobs.View)]
public record ListJobRunsQuery(PagedRequest Request, string? JobName) : IRequest<PagedResponse<JobRunDto>>;

public class ListJobRunsQueryHandler(ApplicationDbContext db) : IRequestHandler<ListJobRunsQuery, PagedResponse<JobRunDto>>
{
    public Task<PagedResponse<JobRunDto>> Handle(ListJobRunsQuery request, CancellationToken ct)
    {
        var query = db.Set<JobRun>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.JobName))
            query = query.Where(r => r.JobName == request.JobName);

        var sortable = new Dictionary<string, Expression<Func<JobRun, object>>>
        {
            ["startedAt"] = r => r.StartedAt,
            ["jobName"] = r => r.JobName,
            ["durationMs"] = r => r.DurationMs
        };

        return query.ApplyPagingAsync(request.Request, sortable,
            r => new JobRunDto(r.Id, r.JobName, r.StartedAt, r.DurationMs, r.Success, r.Error, r.Trigger), ct);
    }
}

[RequiresPermission(Permissions.Jobs.Run)]
public record RunJobCommand(string JobName) : IRequest<bool>, IAuditableCommand;

/// <returns>true if it ran now; false if it was already running.</returns>
public class RunJobCommandHandler(IEnumerable<IRecurringJob> jobs, JobExecutor executor) : IRequestHandler<RunJobCommand, bool>
{
    public async Task<bool> Handle(RunJobCommand request, CancellationToken ct)
    {
        var job = jobs.FirstOrDefault(j => j.Name == request.JobName)
            ?? throw new NotFoundException($"Job '{request.JobName}' was not found.");

        return await executor.RunAsync(job, "Manual", ct);
    }
}
