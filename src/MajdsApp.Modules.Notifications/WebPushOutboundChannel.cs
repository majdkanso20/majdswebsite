using System.Text.Json;
using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebPush;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// Browser push as an <see cref="IOutboundChannel"/> (F-Notifications FR-NOTIF-002/009), built on the W3C Push API and VAPID — a real
/// implementation, not a stub, but one that needs no third-party account: an administrator generates a key pair on the settings page
/// (<see cref="GenerateVapidKeysQuery"/>) and the server signs every push with it. A user can have several subscriptions (one per
/// device); <see cref="ResolveAddressAsync"/> hands all of them to <see cref="SendAsync"/> as one JSON array, which sends to each and
/// forgets any the browser itself reports as gone (a 410 from its push service means that device unsubscribed or was reset).
/// </summary>
public class WebPushOutboundChannel(ApplicationDbContext db, ISettingsProvider settings, ILogger<WebPushOutboundChannel> logger) : IOutboundChannel
{
    public const string ChannelName = "Push";

    public string Name => ChannelName;
    public string DisplayName => "Push notifications";
    public bool EnabledByDefault => false; // needs a subscription a user opts into on a specific device, unlike email

    public async Task<string?> ResolveAddressAsync(string userId, CancellationToken ct = default)
    {
        var subscriptions = await db.Set<PushDeviceSubscription>().AsNoTracking().Where(s => s.UserId == userId)
            .Select(s => new SubscriptionAddress(s.Endpoint, s.P256dh, s.Auth)).ToListAsync(ct);

        return subscriptions.Count == 0 ? null : JsonSerializer.Serialize(subscriptions);
    }

    public async Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct = default)
    {
        var publicKey = await settings.GetAsync(NotificationSettings.Notifications.PushVapidPublicKey.Name, ct);
        var privateKey = await settings.GetAsync(NotificationSettings.Notifications.PushVapidPrivateKey.Name, ct);
        var subject = await settings.GetAsync(NotificationSettings.Notifications.PushVapidSubject.Name, ct);
        if (publicKey.Length == 0 || privateKey.Length == 0 || subject.Length == 0)
            throw new InvalidOperationException("Push notifications need a VAPID key pair and contact address set on the Notifications settings page.");

        var subscriptions = JsonSerializer.Deserialize<List<SubscriptionAddress>>(address) ?? [];
        if (subscriptions.Count == 0) return DeliveryOutcome.Suppressed;

        // Angular's service worker (SwPush on the client) shows a notification straight from a payload shaped like this.
        var payload = JsonSerializer.Serialize(new
        {
            notification = new
            {
                title = message.Title,
                body = message.Message,
                icon = "icons/icon-192.png",
                data = new { }
            }
        });

        var client = new WebPushClient();
        var vapid = new VapidDetails(subject, publicKey, privateKey);
        var sent = 0;
        Exception? retriableFailure = null;

        foreach (var subscription in subscriptions)
        {
            try
            {
                await client.SendNotificationAsync(
                    new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, vapid, ct);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                // The browser's push service says this subscription no longer exists: stop trying it, not a delivery failure to retry.
                await db.Set<PushDeviceSubscription>().Where(s => s.Endpoint == subscription.Endpoint).ExecuteDeleteAsync(ct);
            }
            catch (Exception ex) when (ex is WebPushException or HttpRequestException)
            {
                logger.LogWarning(ex, "Push to one subscription failed");
                retriableFailure = ex;
            }
        }

        // Some devices got it: a real success, even if another of the user's devices failed or was dropped.
        if (sent > 0) return DeliveryOutcome.Sent;

        // Nothing sent, and at least one device failed for a reason that might clear up (not just "no subscriptions left"):
        // throw so the queue retries with backoff, the same as any other channel's transient failure.
        if (retriableFailure is not null) throw retriableFailure;

        return DeliveryOutcome.Suppressed;
    }

    private record SubscriptionAddress(string Endpoint, string P256dh, string Auth);
}
