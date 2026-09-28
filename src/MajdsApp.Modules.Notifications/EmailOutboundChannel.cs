using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// Email as an <see cref="IOutboundChannel"/>: the first channel, and the pattern for any other. It reaches a user at their account's email address and sends through
/// the platform's mail sender, which applies the delivery mode itself (so it is also applied to mail that does not come through notifications).
/// </summary>
public class EmailOutboundChannel(ApplicationDbContext db, IEmailMessageSender? sender = null) : IOutboundChannel
{
    public const string ChannelName = "Email";

    public string Name => ChannelName;
    public string DisplayName => "Email";
    public bool AppliesDeliveryModeItself => true;

    public Task<string?> ResolveAddressAsync(string userId, CancellationToken ct = default) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);

    public Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct = default) =>
        sender is null
            ? throw new InvalidOperationException("Email is not configured on this server.")
            : sender.SendAsync(address, message.Title, message.HtmlBody ?? $"<p>{System.Net.WebUtility.HtmlEncode(message.Message)}</p>", ct);
}
