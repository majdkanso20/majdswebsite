using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>Marks the given notifications read, or all of the caller's when Ids is null. Scoped to the caller.</summary>
[RequiresFeature("Notifications")]
public record MarkNotificationsReadCommand(IReadOnlyList<int>? Ids) : IRequest;

public class MarkNotificationsReadCommandHandler(ApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<MarkNotificationsReadCommand>
{
    public async Task Handle(MarkNotificationsReadCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var query = db.Set<Notification>().Where(n => n.UserId == userId && !n.IsRead);
        if (request.Ids is not null)
            query = query.Where(n => request.Ids.Contains(n.Id));

        await query.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}

/// <summary>Sends a notification to one user, or to every active user when UserId is null.</summary>
[RequiresFeature("Notifications")]
[RequiresPermission(Permissions.Notifications.Send)]
public record SendNotificationCommand(string? UserId, string Title, string Message) : IRequest, IAuditableCommand;

public class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}

public class SendNotificationCommandHandler(IUserNotificationPublisher publisher) : IRequestHandler<SendNotificationCommand>
{
    public Task Handle(SendNotificationCommand request, CancellationToken ct) =>
        request.UserId is null
            ? publisher.PublishToAllAsync(request.Title, request.Message, ct: ct)
            : publisher.PublishAsync(request.UserId, request.Title, request.Message, ct: ct);
}
