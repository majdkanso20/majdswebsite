using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Notifications;

/// <summary>Drains queued deliveries (email) with retry and exponential backoff (FR-NOTIF-007):
/// a failure is retried up to <see cref="MaxAttempts"/> times, then recorded as permanently failed.</summary>
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
        var sender = scope.ServiceProvider.GetService<IEmailMessageSender>();

        var now = DateTime.UtcNow;
        var due = await db.Set<NotificationDelivery>()
            .Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt).Take(20).ToListAsync(ct);

        foreach (var delivery in due)
        {
            delivery.Attempts++;
            try
            {
                if (sender is null) throw new InvalidOperationException("Email is not configured on this server.");

                var email = await db.Users.IgnoreQueryFilters().Where(u => u.Id == delivery.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
                if (string.IsNullOrEmpty(email)) throw new InvalidOperationException("Recipient has no email address.");

                var outcome = await sender.SendAsync(email, delivery.Title, delivery.Body ?? $"<p>{System.Net.WebUtility.HtmlEncode(delivery.Message)}</p>", ct);
                if (outcome == DeliveryOutcome.Suppressed)
                {
                    // Dev mode (or Test with no test recipient): the mail channel logged it instead of sending it.
                    delivery.Status = DeliveryStatus.Suppressed;
                    delivery.LastError = "Not sent: the delivery mode is Dev, or Test with no test recipient.";
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
}
