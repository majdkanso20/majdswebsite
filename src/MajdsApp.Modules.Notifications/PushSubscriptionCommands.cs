using FluentValidation;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace MajdsApp.Modules.Notifications;

/// <summary>What a browser's <c>PushManager.subscribe()</c> gives the page (FR-NOTIF-002/009): the push service URL and the two keys
/// needed to encrypt a message it can decrypt.</summary>
public record SubscribeToPushCommand(string Endpoint, string P256dh, string Auth) : IRequest;

public class SubscribeToPushCommandValidator : AbstractValidator<SubscribeToPushCommand>
{
    public SubscribeToPushCommandValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.P256dh).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Auth).NotEmpty().MaximumLength(200);
    }
}

public class SubscribeToPushCommandHandler(ApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<SubscribeToPushCommand>
{
    public async Task Handle(SubscribeToPushCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");

        var existing = await db.Set<PushDeviceSubscription>().FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == request.Endpoint, ct);
        if (existing is not null)
        {
            // The keys can change if the browser re-subscribes on the same endpoint; keep them current.
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
        }
        else
        {
            db.Set<PushDeviceSubscription>().Add(new PushDeviceSubscription
            {
                UserId = userId, Endpoint = request.Endpoint, P256dh = request.P256dh, Auth = request.Auth, CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>The browser unsubscribed, or the user turned the toggle off — stop sending to this one device.</summary>
public record UnsubscribeFromPushCommand(string Endpoint) : IRequest;

public class UnsubscribeFromPushCommandHandler(ApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<UnsubscribeFromPushCommand>
{
    public async Task Handle(UnsubscribeFromPushCommand request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("Authentication is required.");
        await db.Set<PushDeviceSubscription>().Where(s => s.UserId == userId && s.Endpoint == request.Endpoint).ExecuteDeleteAsync(ct);
    }
}

/// <summary>The public half of the VAPID key pair a subscribing browser needs (<c>PushManager.subscribe({ applicationServerKey })</c>);
/// empty when an administrator has not set one up yet, which the frontend reads as "push is not available here".</summary>
public record GetVapidPublicKeyQuery : IRequest<string?>;

public class GetVapidPublicKeyQueryHandler(ISettingsProvider settings) : IRequestHandler<GetVapidPublicKeyQuery, string?>
{
    public async Task<string?> Handle(GetVapidPublicKeyQuery request, CancellationToken ct)
    {
        var key = await settings.GetAsync(NotificationSettings.Notifications.PushVapidPublicKey.Name, ct);
        return key.Length == 0 ? null : key;
    }
}

public record VapidKeyPairDto(string PublicKey, string PrivateKey);

/// <summary>Generates a fresh VAPID key pair for an administrator to paste into the Push settings (FR-NOTIF-008): push needs no
/// third-party account, only a key pair the server itself signs messages with — this is that, done properly (a real EC key pair),
/// without asking anyone to run their own tooling.</summary>
[RequiresPermission(Permissions.Notifications.Send)]
public record GenerateVapidKeysQuery : IRequest<VapidKeyPairDto>;

public class GenerateVapidKeysQueryHandler : IRequestHandler<GenerateVapidKeysQuery, VapidKeyPairDto>
{
    public Task<VapidKeyPairDto> Handle(GenerateVapidKeysQuery request, CancellationToken ct)
    {
        var keys = VapidHelper.GenerateVapidKeys();
        return Task.FromResult(new VapidKeyPairDto(keys.PublicKey, keys.PrivateKey));
    }
}
