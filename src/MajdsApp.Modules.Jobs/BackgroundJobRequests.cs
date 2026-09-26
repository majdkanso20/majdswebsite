using MajdsApp.SharedKernel.Mapping;
using System.Linq.Expressions;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Jobs;

public record BackgroundJobDto(
    Guid Id, string Type, string Status, int Attempts, int MaxAttempts, string? UserName, string? LastError,
    DateTime CreatedAt, DateTime NextAttemptAt, DateTime? StartedAt, DateTime? CompletedAt);

/// <summary>The queued one-off jobs, newest first, optionally only one status (FR-JOB-003).</summary>
[RequiresPermission(Permissions.Jobs.View)]
public record ListBackgroundJobsQuery(PagedRequest Request, string? Status) : IRequest<PagedResponse<BackgroundJobDto>>;

public class ListBackgroundJobsQueryHandler(ApplicationDbContext db, IObjectMapper mapper) : IRequestHandler<ListBackgroundJobsQuery, PagedResponse<BackgroundJobDto>>
{
    public Task<PagedResponse<BackgroundJobDto>> Handle(ListBackgroundJobsQuery request, CancellationToken ct)
    {
        var query = db.Set<BackgroundJob>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<BackgroundJobStatus>(request.Status, ignoreCase: true, out var status))
                throw new FluentValidation.ValidationException($"'{request.Status}' is not a job status (Pending, Running, Succeeded, Failed).");
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = MajdsApp.SharedKernel.Search.LikePattern.Contains(request.Request.Filter);
            query = query.Where(j => EF.Functions.Like(j.Type, pattern, MajdsApp.SharedKernel.Search.LikePattern.Escape));
        }

        var sortable = new Dictionary<string, Expression<Func<BackgroundJob, object>>>
        {
            ["createdAt"] = j => j.CreatedAt,
            ["type"] = j => j.Type,
            ["attempts"] = j => j.Attempts
        };

        if (string.IsNullOrWhiteSpace(request.Request.Sort)) request.Request.Sort = "createdAt:desc";

        return query.ApplyPagingAsync(request.Request, sortable,
            mapper.Projection<BackgroundJob, BackgroundJobDto>(), ct);
    }
}

/// <summary>Puts a failed job back in the queue with a fresh set of attempts (FR-JOB-003).</summary>
[RequiresPermission(Permissions.Jobs.Manage)]
public record RetryBackgroundJobCommand(Guid JobId) : IRequest, IAuditableCommand;

public class RetryBackgroundJobCommandHandler(ApplicationDbContext db) : IRequestHandler<RetryBackgroundJobCommand>
{
    public async Task Handle(RetryBackgroundJobCommand request, CancellationToken ct)
    {
        var job = await db.Set<BackgroundJob>().FirstOrDefaultAsync(j => j.Id == request.JobId, ct)
            ?? throw new NotFoundException("Job not found.");

        if (job.Status != BackgroundJobStatus.Failed)
            throw new ConflictException("Only a failed job can be retried.");

        job.Status = BackgroundJobStatus.Pending;
        job.Attempts = 0;
        job.NextAttemptAt = DateTime.UtcNow;
        job.CompletedAt = null;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Removes a job from the queue and the history. A job that is running cannot be deleted (FR-JOB-003).</summary>
[RequiresPermission(Permissions.Jobs.Manage)]
public record DeleteBackgroundJobCommand(Guid JobId) : IRequest, IAuditableCommand;

public class DeleteBackgroundJobCommandHandler(ApplicationDbContext db) : IRequestHandler<DeleteBackgroundJobCommand>
{
    public async Task Handle(DeleteBackgroundJobCommand request, CancellationToken ct)
    {
        var job = await db.Set<BackgroundJob>().FirstOrDefaultAsync(j => j.Id == request.JobId, ct)
            ?? throw new NotFoundException("Job not found.");

        if (job.Status == BackgroundJobStatus.Running)
            throw new ConflictException("A running job cannot be deleted.");

        db.Set<BackgroundJob>().Remove(job);
        await db.SaveChangesAsync(ct);
    }
}
