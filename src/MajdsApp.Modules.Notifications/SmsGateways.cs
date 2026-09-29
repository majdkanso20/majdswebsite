using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using Microsoft.Extensions.Logging;

namespace MajdsApp.Modules.Notifications;

/// <summary>
/// One SMS provider's own endpoint and payload shape, hidden behind one method (F-Notifications FR-NOTIF-002/009): the
/// whole point of the interface, per the supervisor's own framing of it, is that <see cref="SmsOutboundChannel"/> neither
/// knows nor cares which of these ran. <see cref="SmsSettings"/>'s <c>Sms.Provider</c> setting picks one by <see cref="Name"/>.
/// </summary>
public interface ISmsGateway
{
    string Name { get; }

    /// <summary>Sends one message. Throw to have it retried later with backoff, the same contract <see cref="IOutboundChannel.SendAsync"/> uses.</summary>
    Task<DeliveryOutcome> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// Twilio's Messages API: HTTP Basic auth (account SID as the username, auth token as the password) and a form-encoded body —
/// a real implementation, not a stub, but (like the Azure Blob file provider) not run against a live account here.
/// </summary>
public class TwilioSmsGateway(ISettingsProvider settings, IHttpClientFactory httpClientFactory, ILogger<TwilioSmsGateway> logger) : ISmsGateway
{
    public string Name => "Twilio";

    public async Task<DeliveryOutcome> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        var accountSid = await settings.GetAsync(SmsSettings.Sms.TwilioAccountSid.Name, ct);
        var authToken = await settings.GetAsync(SmsSettings.Sms.TwilioAuthToken.Name, ct);
        var fromNumber = await settings.GetAsync(SmsSettings.Sms.TwilioFromNumber.Name, ct);
        if (accountSid.Length == 0 || authToken.Length == 0 || fromNumber.Length == 0)
            throw new InvalidOperationException("Twilio needs an account SID, auth token and sender number set on the Sms settings page.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = toPhoneNumber, ["From"] = fromNumber, ["Body"] = message })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}")));

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return DeliveryOutcome.Sent;

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("Twilio SMS send failed: {Status} {Body}", response.StatusCode, body);
        throw new HttpRequestException($"Twilio refused the message ({(int)response.StatusCode}): {body}");
    }
}

/// <summary>
/// Vonage's SMS API: a JSON body carrying the API key and secret rather than an auth header, and (unlike Twilio) a 200
/// response even for many failures — the real result is the per-message status code inside the body. A different shape
/// end to end from Twilio's, which is the point: the channel above never has to know that.
/// </summary>
public class VonageSmsGateway(ISettingsProvider settings, IHttpClientFactory httpClientFactory, ILogger<VonageSmsGateway> logger) : ISmsGateway
{
    public string Name => "Vonage";

    public async Task<DeliveryOutcome> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        var apiKey = await settings.GetAsync(SmsSettings.Sms.VonageApiKey.Name, ct);
        var apiSecret = await settings.GetAsync(SmsSettings.Sms.VonageApiSecret.Name, ct);
        var fromNumber = await settings.GetAsync(SmsSettings.Sms.VonageFromNumber.Name, ct);
        if (apiKey.Length == 0 || apiSecret.Length == 0 || fromNumber.Length == 0)
            throw new InvalidOperationException("Vonage needs an API key, API secret and sender ID set on the Sms settings page.");

        var payload = JsonSerializer.Serialize(new { api_key = apiKey, api_secret = apiSecret, to = toPhoneNumber.TrimStart('+'), from = fromNumber, text = message });

        using var client = httpClientFactory.CreateClient();
        using var response = await client.PostAsync("https://rest.nexmo.com/sms/json", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Vonage SMS send failed: {Status} {Body}", response.StatusCode, body);
            throw new HttpRequestException($"Vonage refused the message ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("messages")[0];
        var status = first.GetProperty("status").GetString();
        if (status != "0")
        {
            var errorText = first.TryGetProperty("error-text", out var e) ? e.GetString() : "unknown error";
            logger.LogWarning("Vonage SMS send failed: status {Status} ({Error})", status, errorText);
            throw new InvalidOperationException($"Vonage refused the message (status {status}): {errorText}");
        }

        return DeliveryOutcome.Sent;
    }
}
