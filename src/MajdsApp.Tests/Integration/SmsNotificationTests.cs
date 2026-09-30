using FluentAssertions;
using MajdsApp.Modules.Notifications;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>
/// F-Notifications FR-NOTIF-002/009: SMS as a real, pluggable channel. The supervisor's own framing of the request was
/// "I can change the underlying implementation of SMS based upon the provider (as every SMS provider provides their own
/// endpoints with their specific payloads)" — <see cref="SmsOutboundChannel"/> never talks to a provider itself, only to
/// whichever <see cref="ISmsGateway"/> the <c>Sms.Provider</c> setting names; Twilio, Vonage and Infobip are the three
/// built in, each with a genuinely different request shape (Basic-auth form post, an API-key JSON body, and an
/// App-scheme auth header with its own per-account base URL).
/// </summary>
public class SmsOutboundChannelTests
{
    private sealed class FixedSettings(Dictionary<string, string> values) : ISettingsProvider
    {
        public Task<string> GetAsync(string name, CancellationToken ct = default) => Task.FromResult(values.GetValueOrDefault(name, ""));
        public async Task<bool> GetBooleanAsync(string name, CancellationToken ct = default) => bool.TryParse(await GetAsync(name, ct), out var v) && v;
        public async Task<int> GetIntegerAsync(string name, CancellationToken ct = default) => int.TryParse(await GetAsync(name, ct), out var v) ? v : 0;
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, string>>(values);
        public Task<IReadOnlyDictionary<string, string>> GetAllForUserAsync(string? userId, CancellationToken ct = default) => GetAllAsync(ct);
        public void Invalidate() { }
        public void InvalidateUser(string userId) { }
    }

    private sealed class RecordingGateway(string name) : ISmsGateway
    {
        public string Name => name;
        public List<(string To, string Message)> Sent { get; } = [];

        public Task<DeliveryOutcome> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default)
        {
            Sent.Add((toPhoneNumber, message));
            return Task.FromResult(DeliveryOutcome.Sent);
        }
    }

    [Fact]
    public async Task With_no_provider_chosen_sending_is_refused_clearly()
    {
        var channel = new SmsOutboundChannel(null!, new FixedSettings([]), []);

        var sending = async () => await channel.SendAsync("+15551234567", new OutboundMessage("u", "General", "Title", "Message", null));

        (await sending.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("provider");
    }

    [Fact]
    public async Task A_provider_chosen_with_no_gateway_registered_for_it_is_refused_by_name()
    {
        var channel = new SmsOutboundChannel(null!, new FixedSettings(new() { ["Sms.Provider"] = "Twilio" }), []);

        var sending = async () => await channel.SendAsync("+15551234567", new OutboundMessage("u", "General", "Title", "Message", null));

        (await sending.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("Twilio");
    }

    [Fact]
    public async Task The_provider_named_by_the_setting_alone_decides_which_gateway_sends()
    {
        var twilio = new RecordingGateway("Twilio");
        var vonage = new RecordingGateway("Vonage");
        var channel = new SmsOutboundChannel(null!, new FixedSettings(new() { ["Sms.Provider"] = "Vonage" }), [twilio, vonage]);

        var outcome = await channel.SendAsync("+15551234567", new OutboundMessage("u", "General", "Title", "Message", null));

        outcome.Should().Be(DeliveryOutcome.Sent);
        vonage.Sent.Should().ContainSingle(s => s.To == "+15551234567" && s.Message == "Message");
        twilio.Sent.Should().BeEmpty(); // changing the setting to "Twilio", not the code, is what would route here instead
    }
}

/// <summary>The same channel wired into the real host: its settings, its user-facing phone number, and each gateway's own
/// missing-credentials error, none of which is run against a live Twilio, Vonage or Infobip account here.</summary>
public class SmsNotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_built_in_providers_settings_appear_under_their_own_Sms_group()
    {
        var admin = await factory.SignInAsync("sms.admin1@example.com", "Admin");

        var listed = (await admin.GetAsync<List<SettingRow>>("/api/settings/list")).Data!;

        listed.Where(s => s.Group == "Sms").Select(s => s.Name).Should().BeEquivalentTo(
            "Sms.Provider", "Sms.Twilio.AccountSid", "Sms.Twilio.AuthToken", "Sms.Twilio.FromNumber",
            "Sms.Vonage.ApiKey", "Sms.Vonage.ApiSecret", "Sms.Vonage.FromNumber",
            "Sms.Infobip.BaseUrl", "Sms.Infobip.ApiKey", "Sms.Infobip.FromNumber");
    }

    [Theory]
    [InlineData("Twilio")]
    [InlineData("Vonage")]
    [InlineData("Infobip")]
    public async Task A_provider_chosen_with_no_credentials_saved_yet_is_refused_by_name_not_a_raw_HTTP_error(string provider)
    {
        using var freshFactory = new ApiFactory();
        var admin = await freshFactory.SignInAsync($"sms.admin2.{provider.ToLowerInvariant()}@example.com", "Admin");
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Sms.Provider", value = provider } } });

        using var scope = freshFactory.Services.CreateScope();
        var channel = scope.ServiceProvider.GetServices<IOutboundChannel>().Single(c => c.Name == "Sms");

        var sending = async () => await channel.SendAsync("+15551234567", new OutboundMessage("u", "General", "Title", "Message", null));
        (await sending.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain(provider);
    }

    [Fact]
    public async Task A_user_s_saved_phone_number_resolves_the_address_and_none_resolves_to_null()
    {
        var admin = await factory.SignInAsync("sms.admin3@example.com", "Admin");
        var user = await factory.SignInAsync("sms.user3@example.com");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=sms.user3")).Data!.Items.Single().Id;

        using (var scope = factory.Services.CreateScope())
        {
            var channel = scope.ServiceProvider.GetServices<IOutboundChannel>().Single(c => c.Name == "Sms");
            (await channel.ResolveAddressAsync(userId)).Should().BeNull();
        }

        (await user.PostAsync("/api/account/update-profile", new { fullName = (string?)null, phoneNumber = "+15559876543" })).Status
            .Should().Be(System.Net.HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var channel = scope.ServiceProvider.GetServices<IOutboundChannel>().Single(c => c.Name == "Sms");
            (await channel.ResolveAddressAsync(userId)).Should().Be("+15559876543");
        }
    }
}
