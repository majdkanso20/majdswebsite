using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>One channel a module added, and whether this user receives this notification type on it.</summary>
public record ChannelChoiceDto(string Name, string DisplayName, bool Enabled);

/// <param name="InApp">In-app and email are built in and keep their own fields.</param>
/// <param name="Channels">Every other channel a module registered (<see cref="IOutboundChannel"/>), so the preferences screen can offer a column for each.</param>
public record SubscriptionDto(string Type, bool InApp, bool Email, IReadOnlyList<ChannelChoiceDto>? Channels = null);

/// <summary>The caller's per-type channel choices; types they never touched default to every built-in channel on and each module channel at its own default.</summary>
[RequiresFeature("Notifications")]
public record GetMySubscriptionsQuery : IRequest<IReadOnlyList<SubscriptionDto>>;

public class GetMySubscriptionsQueryHandler(ApplicationDbContext db, ICurrentUser currentUser, IEnumerable<IOutboundChannel> outbound)
    : IRequestHandler<GetMySubscriptionsQuery, IReadOnlyList<SubscriptionDto>>
{
    public async Task<IReadOnlyList<SubscriptionDto>> Handle(GetMySubscriptionsQuery request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var saved = await db.Set<NotificationSubscription>().AsNoTracking().Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.Type, s => s.Channels, ct);
        var extra = (await db.Set<NotificationChannelChoice>().AsNoTracking().Where(c => c.UserId == userId).ToListAsync(ct))
            .ToDictionary(c => (c.Type, c.ChannelName), c => c.Enabled);
        var moduleChannels = outbound.Where(c => c.Name != EmailOutboundChannel.ChannelName).ToList();

        return NotificationTypes.All.Select(type =>
        {
            var channels = saved.TryGetValue(type, out var c) ? c : NotificationChannel.All;
            var others = moduleChannels.Select(ch =>
                new ChannelChoiceDto(ch.Name, ch.DisplayName, extra.TryGetValue((type, ch.Name), out var on) ? on : ch.EnabledByDefault)).ToList();
            return new SubscriptionDto(type, channels.HasFlag(NotificationChannel.InApp), channels.HasFlag(NotificationChannel.Email), others);
        }).ToList();
    }
}

[RequiresFeature("Notifications")]
public record UpdateMySubscriptionsCommand(IReadOnlyList<SubscriptionDto> Items) : IRequest;

public class UpdateMySubscriptionsCommandHandler(ApplicationDbContext db, ICurrentUser currentUser, IEnumerable<IOutboundChannel> outbound)
    : IRequestHandler<UpdateMySubscriptionsCommand>
{
    public async Task Handle(UpdateMySubscriptionsCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        var known = outbound.Where(c => c.Name != EmailOutboundChannel.ChannelName).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // A type or channel the client made up is an error, not something to skip quietly (F-Account FR-ACC-003).
        foreach (var unknown in request.Items.Select(i => i.Type).Distinct().Where(t => !NotificationTypes.All.Contains(t)))
            throw new FluentValidation.ValidationException($"'{unknown}' is not a notification type.");
        foreach (var unknown in request.Items.SelectMany(i => i.Channels ?? []).Select(c => c.Name).Distinct().Where(n => !known.ContainsKey(n)))
            throw new FluentValidation.ValidationException($"'{unknown}' is not a notification channel.");

        var existing = await db.Set<NotificationSubscription>().Where(s => s.UserId == userId).ToListAsync(ct);
        var existingExtra = await db.Set<NotificationChannelChoice>().Where(c => c.UserId == userId).ToListAsync(ct);

        foreach (var item in request.Items)
        {
            var channels = (item.InApp ? NotificationChannel.InApp : NotificationChannel.None)
                | (item.Email ? NotificationChannel.Email : NotificationChannel.None);
            var row = existing.FirstOrDefault(s => s.Type == item.Type);

            // Every channel on is the default, so choosing it removes the stored choice rather than keeping a row that says the same thing.
            if (channels == NotificationChannel.All)
            {
                if (row is not null) db.Set<NotificationSubscription>().Remove(row);
            }
            else if (row is null)
                db.Set<NotificationSubscription>().Add(new NotificationSubscription { UserId = userId, Type = item.Type, Channels = channels });
            else
                row.Channels = channels;

            // The same rule for each module channel: a row is kept only when the choice differs from that channel's default.
            foreach (var choice in item.Channels ?? [])
            {
                var channel = known[choice.Name];
                var stored = existingExtra.FirstOrDefault(c => c.Type == item.Type && c.ChannelName.Equals(channel.Name, StringComparison.OrdinalIgnoreCase));
                if (choice.Enabled == channel.EnabledByDefault)
                {
                    if (stored is not null) db.Set<NotificationChannelChoice>().Remove(stored);
                }
                else if (stored is null)
                    db.Set<NotificationChannelChoice>().Add(new NotificationChannelChoice { UserId = userId, Type = item.Type, ChannelName = channel.Name, Enabled = choice.Enabled });
                else
                    stored.Enabled = choice.Enabled;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
