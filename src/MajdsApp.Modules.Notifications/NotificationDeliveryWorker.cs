using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// Delivers queued notifications through whichever <see cref="IOutboundChannel"/> each one names (F-Notifications FR-NOTIF-004/007/009), with retry and exponential
/// backoff. It knows nothing about email or SMS: a channel is anything that implements the interface. The delivery mode (Dev, Test or Prod) is applied here for every channel that
/// does not apply it itself, so a new channel obeys it without writing any code for it.
/// </summary>
public class NotificationDeliveryWorker(IServiceScopeFactory scopes, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Notification delivery batch failed"); }

            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var channels = scope.ServiceProvider.GetServices<IOutboundChannel>().ToList();
        var policy = scope.ServiceProvider.GetService<IDeliveryModePolicy>();

        var now = DateTime.UtcNow;
        var due = await db.Set<NotificationDelivery>()
            .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt).Take(20).ToListAsync(ct);

        foreach (var delivery in due)
        {
            delivery.Attempts++;
            try
            {
                // Deliveries made before channels were pluggable have no name; they are email.
                var name = delivery.ChannelName ?? EmailOutboundChannel.ChannelName;
                var channel = channels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"The {name} channel is not available on this server.");

                var address = await channel.ResolveAddressAsync(delivery.UserId, ct);
                if (string.IsNullOrEmpty(address)) throw new InvalidOperationException($"Recipient has no {channel.DisplayName} address.");

                var title = delivery.Title;
                var realAddress = address;
                if (!channel.AppliesDeliveryModeItself && policy is not null)
                {
                    var decision = await policy.DecideAsync(channel.Name, address, ct);
                    if (decision.Recipient is null)
                    {
                        logger.LogInformation("[{Mode}] {Channel} message NOT sent ({Reason}). It was for {To}: '{Title}'", decision.Mode, channel.Name, decision.Reason, address, title);
                        MarkSuppressed(delivery);
                        continue;
                    }

                    address = decision.Recipient;
                    title = (decision.SubjectPrefix ?? "") + title;
                    if (decision.Mode == DeliveryMode.Test) logger.LogInformation("[Test] {Channel} message for {Real} goes to the test recipient {Test} instead", channel.Name, realAddress, address);
                }

                var outcome = await channel.SendAsync(address, new OutboundMessage(delivery.UserId, delivery.Type, title, delivery.Message, delivery.Body), ct);
                if (outcome == DeliveryOutcome.Suppressed)
                {
                    MarkSuppressed(delivery);
                }
                else
                {
                    delivery.Status = DeliveryStatus.Sent;
                    delivery.SentAt = DateTime.UtcNow;
                    delivery.LastError = null;
                }
            }
            catch (Exception ex)
            {
                delivery.LastError = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
                if (delivery.Attempts >= MaxAttempts)
                    delivery.Status = DeliveryStatus.Failed;
                else
                    delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(10 * Math.Pow(2, delivery.Attempts));
            }
        }

        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }

    // Dev mode (or Test with no test recipient): the channel logged the message instead of sending it.
    private static void MarkSuppressed(NotificationDelivery delivery)
    {
        delivery.Status = DeliveryStatus.Suppressed;
        delivery.LastError = "Not sent: the delivery mode is Dev, or Test with no test recipient.";
    }
}
