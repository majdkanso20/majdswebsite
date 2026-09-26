using MajdsApp.SharedKernel.Notifications;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

public record SubscriptionDto(string Type, bool InApp, bool Email);

/// <summary>The caller's per-type channel choices; types they never touched default to every channel on.</summary>
[RequiresFeature("Notifications")]
public record GetMySubscriptionsQuery : IRequest<IReadOnlyList<SubscriptionDto>>;

public class GetMySubscriptionsQueryHandler(ApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMySubscriptionsQuery, IReadOnlyList<SubscriptionDto>>
{
    public async Task<IReadOnlyList<SubscriptionDto>> Handle(GetMySubscriptionsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var saved = await db.Set<NotificationSubscription>().AsNoTracking().Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Type, s => s.Channels, ct);

        return NotificationTypes.All.Select(type =>
        {
            var channels = saved.TryGetValue(type, out var c) ? c : NotificationChannel.All;
            return new SubscriptionDto(type, channels.HasFlag(NotificationChannel.InApp), channels.HasFlag(NotificationChannel.Email));
        }).ToList();
    }
}

[RequiresFeature("Notifications")]
public record UpdateMySubscriptionsCommand(IReadOnlyList<SubscriptionDto> Items) : IRequest;

public class UpdateMySubscriptionsCommandHandler(ApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateMySubscriptionsCommand>
{
    public async Task Handle(UpdateMySubscriptionsCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var existing = await db.Set<NotificationSubscription>().Where(s => s.UserId == userId).ToListAsync(ct);

        // A type the client made up is an error, not something to skip quietly (F-Account FR-ACC-003).
        foreach (var unknown in request.Items.Select(i => i.Type).Distinct().Where(t => !NotificationTypes.All.Contains(t)))
            throw new FluentValidation.ValidationException($"'{unknown}' is not a notification type.");

        foreach (var item in request.Items)
        {
            var channels = (item.InApp ? NotificationChannel.InApp : NotificationChannel.None)
                | (item.Email ? NotificationChannel.Email : NotificationChannel.None);
            var row = existing.FirstOrDefault(s => s.Type == item.Type);

            // Every channel on is the default, so choosing it removes the stored choice rather than keeping a row that says the same thing.
            if (channels == NotificationChannel.All)
            {
                if (row is not null) db.Set<NotificationSubscription>().Remove(row);
                continue;
            }

            if (row is null)
                db.Set<NotificationSubscription>().Add(new NotificationSubscription { UserId = userId, Type = item.Type, Channels = channels });
            else
                row.Channels = channels;
        }

        await db.SaveChangesAsync(ct);
    }
}
