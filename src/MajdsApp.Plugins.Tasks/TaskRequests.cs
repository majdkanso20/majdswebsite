using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Paging;
using MajdsApp.SharedKernel.Search;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Plugins.Tasks;

public record TaskDto(Guid Id, string Title, string? Description, string Status, DateTime? DueDate, DateTime CreatedAt);

/// <summary>Deliberately throws (P5 FR-PLUG-037): proves that an unhandled exception in a plugin's own handler is
/// contained by the host's exception-handling middleware (F-Errors) as a safe <c>ResponseDto</c>, same as a core module,
/// and neither crashes the host nor takes any other plugin down with it.</summary>
[RequiresPermission(Permissions.Tasks.View)]
public record BoomQuery : IRequest<string>;

public class BoomQueryHandler : IRequestHandler<BoomQuery, string>
{
    public Task<string> Handle(BoomQuery request, CancellationToken ct) =>
        throw new InvalidOperationException("Deliberate diagnostic failure (Tasks plugin).");
}

[RequiresPermission(Permissions.Tasks.View)]
public record ListTasksQuery(PagedRequest Request) : IRequest<PagedResponse<TaskDto>>;

public class ListTasksQueryHandler(ApplicationDbContext db) : IRequestHandler<ListTasksQuery, PagedResponse<TaskDto>>
{
    public Task<PagedResponse<TaskDto>> Handle(ListTasksQuery request, CancellationToken ct)
    {
        var query = db.Set<TaskItem>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Request.Filter))
        {
            var pattern = LikePattern.Contains(request.Request.Filter);
            query = query.Where(t => EF.Functions.Like(t.Title, pattern, LikePattern.Escape));
        }

        var sortableColumns = new Dictionary<string, System.Linq.Expressions.Expression<Func<TaskItem, object>>>
        {
            ["title"] = t => t.Title,
            ["status"] = t => t.Status,
            ["dueDate"] = t => t.DueDate!,
            ["createdAt"] = t => t.CreatedAt
        };

        return query.ApplyPagingAsync(request.Request, sortableColumns, t =>
            new TaskDto(t.Id, t.Title, t.Description, t.Status.ToString(), t.DueDate, t.CreatedAt), ct);
    }
}

[RequiresPermission(Permissions.Tasks.View)]
public record GetTaskQuery(Guid Id) : IRequest<TaskDto>;

public class GetTaskQueryHandler(ApplicationDbContext db) : IRequestHandler<GetTaskQuery, TaskDto>
{
    public async Task<TaskDto> Handle(GetTaskQuery request, CancellationToken ct)
    {
        var task = await db.Set<TaskItem>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Task not found.");

        return new TaskDto(task.Id, task.Title, task.Description, task.Status.ToString(), task.DueDate, task.CreatedAt);
    }
}

[RequiresPermission(Permissions.Tasks.Create)]
public record CreateTaskCommand(string Title, string? Description, string Status, DateTime? DueDate) : IRequest<Guid>, IAuditableCommand;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator(ISettingsProvider settings)
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Title).MustAsync(async (title, ct) => title is null || title.Length <= await settings.GetIntegerAsync(TaskSettingDefinitions.Tasks.MaxTitleLength.Name, ct))
            .WithMessage("The title is longer than the limit set for tasks.");
        RuleFor(x => x.Status).Must(s => Enum.TryParse<TaskItemStatus>(s, out _)).WithMessage("Invalid status.");
    }
}

public class CreateTaskCommandHandler(ApplicationDbContext db) : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Status = Enum.Parse<TaskItemStatus>(request.Status),
            DueDate = request.DueDate
        };

        db.Set<TaskItem>().Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }
}

[RequiresPermission(Permissions.Tasks.Edit)]
public record UpdateTaskCommand(Guid Id, string Title, string? Description, string Status, DateTime? DueDate) : IRequest, IAuditableCommand;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator(ISettingsProvider settings)
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Title).MustAsync(async (title, ct) => title is null || title.Length <= await settings.GetIntegerAsync(TaskSettingDefinitions.Tasks.MaxTitleLength.Name, ct))
            .WithMessage("The title is longer than the limit set for tasks.");
        RuleFor(x => x.Status).Must(s => Enum.TryParse<TaskItemStatus>(s, out _)).WithMessage("Invalid status.");
    }
}

public class UpdateTaskCommandHandler(ApplicationDbContext db) : IRequestHandler<UpdateTaskCommand>
{
    public async Task Handle(UpdateTaskCommand request, CancellationToken ct)
    {
        var task = await db.Set<TaskItem>().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Task not found.");

        task.Title = request.Title;
        task.Description = request.Description;
        task.Status = Enum.Parse<TaskItemStatus>(request.Status);
        task.DueDate = request.DueDate;

        await db.SaveChangesAsync(ct);
    }
}

[RequiresPermission(Permissions.Tasks.Delete)]
public record DeleteTaskCommand(Guid Id) : IRequest, IAuditableCommand;

public class DeleteTaskCommandHandler(ApplicationDbContext db) : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        var task = await db.Set<TaskItem>().FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException("Task not found.");

        db.Set<TaskItem>().Remove(task);
        await db.SaveChangesAsync(ct);
    }
}
