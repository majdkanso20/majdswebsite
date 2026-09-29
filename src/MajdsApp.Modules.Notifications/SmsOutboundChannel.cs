using MajdsApp.Data;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// SMS as an <see cref="IOutboundChannel"/> (F-Notifications FR-NOTIF-002/009): reaches a user at their account's phone
/// number and hands the message to whichever <see cref="ISmsGateway"/> the <c>Sms.Provider</c> setting names. The channel
/// itself never talks to a provider directly — swapping Twilio for Vonage, or adding a third gateway, is a setting change
/// and a DI registration, not a change here, which is exactly the pluggability every other channel already has.
/// </summary>
public class SmsOutboundChannel(ApplicationDbContext db, ISettingsProvider settings, IEnumerable<ISmsGateway> gateways) : IOutboundChannel
{
    public const string ChannelName = "Sms";

    public string Name => ChannelName;
    public string DisplayName => "SMS";
    public bool EnabledByDefault => false; // costs money and needs a phone number on file, like push needs a subscription

    public Task<string?> ResolveAddressAsync(string userId, CancellationToken ct = default) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(ct);

    public async Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct = default)
    {
        var providerName = await settings.GetAsync(SmsSettings.Sms.Provider.Name, ct);
        if (providerName.Length == 0)
            throw new InvalidOperationException("SMS needs a provider chosen on the Sms settings page.");

        var gateway = gateways.FirstOrDefault(g => g.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (gateway is null)
            throw new InvalidOperationException($"SMS provider '{providerName}' is not available.");

        return await gateway.SendAsync(address, message.Message, ct);
    }
}
