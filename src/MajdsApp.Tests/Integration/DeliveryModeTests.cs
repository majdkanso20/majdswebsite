using System.Net;
using FluentAssertions;
using MajdsApp.Configuration;
using MajdsApp.Data;
using MajdsApp.Modules.Notifications;
using MajdsApp.Services;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Settings;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>The delivery environment for notification channels: Dev logs only, Test redirects to a test recipient, Prod sends to the real recipient.</summary>
public class DeliveryModeTests
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

    private static SettingsDeliveryModePolicy Policy(params (string Name, string Value)[] values) =>
        new(new FixedSettings(values.ToDictionary(v => v.Name, v => v.Value)));

    [Fact]
    public async Task With_nothing_configured_mail_goes_to_the_real_recipient_as_before()
    {
        var decision = await Policy().DecideAsync("Email", "real@example.com");

        decision.Should().Be(new DeliveryDecision(DeliveryMode.Prod, "real@example.com", null, null));
        decision.Outcome("real@example.com").Should().Be(DeliveryOutcome.Sent);
    }

    [Fact]
    public async Task Dev_sends_nothing_and_says_why()
    {
        var decision = await Policy(("Notifications.DeliveryMode", "Dev")).DecideAsync("Email", "real@example.com");

        decision.Recipient.Should().BeNull();
        decision.Reason.Should().Contain("Dev");
        decision.Outcome("real@example.com").Should().Be(DeliveryOutcome.Suppressed);
    }

    [Fact]
    public async Task Test_sends_only_to_the_test_recipient_and_marks_who_it_was_for()
    {
        var decision = await Policy(("Notifications.DeliveryMode", "Test"), ("Notifications.Email.TestRecipient", "qa@example.com")).DecideAsync("Email", "real@example.com");

        decision.Recipient.Should().Be("qa@example.com");
        decision.SubjectPrefix.Should().Be("[Test for real@example.com] ");
        decision.Outcome("real@example.com").Should().Be(DeliveryOutcome.Redirected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Test_with_no_test_recipient_sends_nothing_rather_than_falling_back_to_the_real_person(string testRecipient)
    {
        var decision = await Policy(("Notifications.DeliveryMode", "Test"), ("Notifications.Email.TestRecipient", testRecipient)).DecideAsync("Email", "real@example.com");

        decision.Recipient.Should().BeNull();
        decision.Reason.Should().Contain("no test recipient");
    }

    [Fact]
    public async Task A_mode_nobody_recognises_sends_nothing()
    {
        var decision = await Policy(("Notifications.DeliveryMode", "Production")).DecideAsync("Email", "real@example.com");

        decision.Recipient.Should().BeNull();
        decision.Reason.Should().Contain("Production");
    }

    [Fact]
    public async Task A_channel_can_have_its_own_mode_which_beats_the_general_one_and_channels_do_not_share_test_recipients()
    {
        var policy = Policy(("Notifications.DeliveryMode", "Prod"), ("Notifications.Email.DeliveryMode", "Dev"), ("Notifications.Sms.DeliveryMode", "Test"), ("Notifications.Sms.TestRecipient", "+10000000"));

        (await policy.DecideAsync("Email", "a@b.c")).Recipient.Should().BeNull();                 // email is in Dev
        (await policy.DecideAsync("Sms", "+15550001")).Recipient.Should().Be("+10000000");       // sms is in Test, to its own test number
        (await policy.DecideAsync("Push", "device-1")).Recipient.Should().Be("device-1");         // a channel with no setting follows the general Prod
    }

    [Fact]
    public async Task The_mail_sender_logs_instead_of_sending_in_dev_and_never_connects()
    {
        var log = new List<string>();
        var services = new ServiceCollection()
            .AddSingleton<ISettingsProvider>(new FixedSettings(new() { ["Notifications.DeliveryMode"] = "Dev" }))
            .AddScoped<IDeliveryModePolicy, SettingsDeliveryModePolicy>()
            .BuildServiceProvider();
        // A host that cannot be reached: if the sender tried to connect, this would throw instead of returning.
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions { Host = "127.0.0.1", Port = 1 }), new CapturingLogger<SmtpEmailSender>(log), services);

        var outcome = await sender.SendAsync("real@example.com", "Hello", "<p>Body</p>");

        outcome.Should().Be(DeliveryOutcome.Suppressed);
        log.Should().ContainSingle(l => l.Contains("NOT sent") && l.Contains("real@example.com") && l.Contains("Hello") && l.Contains("Dev"));
    }

    private sealed class CapturingLogger<T>(List<string> lines) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => lines.Add(formatter(state, exception));
    }

    // ---- the whole path, through the settings page and the background worker ---------------------------------------------------

    private sealed class MailConfigured() : ApiFactory(new Dictionary<string, string> { ["Email:Smtp:Username"] = "u", ["Email:Smtp:Password"] = "p" }, null);

    [Fact]
    public async Task In_dev_mode_a_queued_notification_email_is_marked_suppressed_and_no_mail_is_sent()
    {
        using var factory = new MailConfigured();
        var admin = await factory.SignInAsync("dm.admin@example.com", "Admin");
        var recipient = await factory.SignInAsync("dm.recipient@example.com");
        var recipientId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=dm.recipient@example.com")).Data!.Items.Single().Id;
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Dev" } } })).Status.Should().Be(HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>().PublishAsync(recipientId, "Your export is ready", "It is waiting for you.");

        NotificationDelivery? delivery = null;
        for (var i = 0; i < 40 && delivery?.Status is null or DeliveryStatus.Pending; i++)
        {
            await Task.Delay(500);
            using var scope = factory.Services.CreateScope();
            delivery = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationDelivery>().AsNoTracking()
                .Where(d => d.UserId == recipientId && d.Channel == NotificationChannel.Email).OrderByDescending(d => d.Id).FirstOrDefaultAsync();
        }

        delivery.Should().NotBeNull();
        delivery!.Status.Should().Be(DeliveryStatus.Suppressed);
        delivery.LastError.Should().Contain("delivery mode");
        recipient.Should().NotBeNull();
    }

    [Fact]
    public async Task The_mode_is_a_setting_on_the_settings_page_that_only_accepts_dev_test_or_prod()
    {
        using var factory = new ApiFactory();
        var admin = await factory.SignInAsync("dm.admin2@example.com", "Admin");

        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Staging" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.Email.TestRecipient", value = "not-an-address" } } })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.Email.DeliveryMode", value = "" } } })).Status.Should().Be(HttpStatusCode.OK);

        var listed = (await admin.GetAsync<List<SettingRow>>("/api/settings/list")).Data!;
        listed.Where(s => s.Group == "Notifications").Select(s => s.Name).Should().BeEquivalentTo(
            "Notifications.DeliveryMode", "Notifications.Email.DeliveryMode", "Notifications.Email.TestRecipient",
            "Notifications.Push.DeliveryMode", "Notifications.Push.VapidPublicKey", "Notifications.Push.VapidPrivateKey", "Notifications.Push.VapidSubject",
            "Notifications.Sms.DeliveryMode", "Notifications.Sms.TestRecipient");
    }
}
