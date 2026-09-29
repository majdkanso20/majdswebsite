using System.Net;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Notifications;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>What a module writes to add a notification channel: one class, one registration. A stand-in for a channel this
/// test suite owns and can misconfigure freely — Email, Push and Sms are all real channels now.</summary>
public class FakeChatChannel : IOutboundChannel
{
    public static readonly List<(string Address, string Title, string Message)> Sent = [];

    public string Name => "Chat";
    public string DisplayName => "Chat";
    public bool EnabledByDefault => false;

    public Task<string?> ResolveAddressAsync(string userId, CancellationToken ct = default) => Task.FromResult<string?>("+1555" + Math.Abs(userId.GetHashCode()).ToString()[..4]);

    public Task<DeliveryOutcome> SendAsync(string address, OutboundMessage message, CancellationToken ct = default)
    {
        lock (Sent) Sent.Add((address, message.Title, message.Message));
        return Task.FromResult(DeliveryOutcome.Sent);
    }
}

public class ChannelFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddScoped<IOutboundChannel, FakeChatChannel>());
    }
}

/// <summary>F-Notifications FR-NOTIF-002/009: a new channel is added by registering a class, with no change to the notification module.</summary>
public class OutboundChannelTests(ChannelFactory factory) : IClassFixture<ChannelFactory>
{
    private record Choice(string Name, string DisplayName, bool Enabled);
    private record Sub(string Type, bool InApp, bool Email, List<Choice>? Channels);

    private async Task<string> IdOfAsync(ApiClient admin, string email) =>
        (await admin.GetAsync<PagedData<UserRow>>($"/api/users/list?page=1&pageSize=5&filter={email}")).Data!.Items.Single().Id;

    private static async Task<NotificationDelivery?> WaitForDeliveryAsync(ChannelFactory factory, string userId, string channel, Func<NotificationDelivery, bool> done)
    {
        NotificationDelivery? delivery = null;
        for (var i = 0; i < 40; i++)
        {
            using var scope = factory.Services.CreateScope();
            delivery = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationDelivery>().AsNoTracking()
                .Where(d => d.UserId == userId && d.ChannelName == channel).OrderByDescending(d => d.Id).FirstOrDefaultAsync();
            if (delivery is not null && done(delivery)) break;
            await Task.Delay(500);
        }

        return delivery;
    }

    [Fact]
    public async Task The_new_channel_appears_in_the_preferences_at_its_own_default_and_a_choice_is_stored_only_when_it_differs()
    {
        var admin = await factory.SignInAsync("oc.admin1@example.com", "Admin");
        var user = await factory.SignInAsync("oc.user1@example.com");
        var userId = await IdOfAsync(admin, "oc.user1@example.com");

        var before = (await user.GetAsync<List<Sub>>("/api/notifications/subscriptions/get")).Data!;
        // Alongside the test's own fake Chat channel, the real Push and Sms channels this session added are registered too (their own default off, no subscription/number for this user).
        before.Should().OnlyContain(s => s.Channels!.Single(c => c.Name == "Chat").DisplayName == "Chat" && !s.Channels!.Single(c => c.Name == "Chat").Enabled);

        await user.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "General", inApp = true, email = true, channels = new[] { new { name = "Chat", displayName = "Chat", enabled = true } } } } });
        (await user.GetAsync<List<Sub>>("/api/notifications/subscriptions/get")).Data!.Single(s => s.Type == "General").Channels!.Single(c => c.Name == "Chat").Enabled.Should().BeTrue();

        var reset = await user.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "General", inApp = true, email = true, channels = new[] { new { name = "Chat", displayName = "Chat", enabled = false } } } } });
        reset.Status.Should().Be(HttpStatusCode.OK, string.Join("; ", reset.Errors));
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationChannelChoice>().Where(c => c.UserId == userId && c.Type == "General" && c.ChannelName == "Chat").ToListAsync())
            .Should().BeEmpty("the default needs no stored row");
    }

    [Fact]
    public async Task A_channel_that_does_not_exist_is_refused()
    {
        var user = await factory.SignInAsync("oc.user2@example.com");

        var response = await user.PostAsync("/api/notifications/subscriptions/update",
            new { items = new[] { new { type = "General", inApp = true, email = true, channels = new[] { new { name = "Pigeon", displayName = "Pigeon", enabled = true } } } } });

        response.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_user_who_turned_the_channel_on_receives_notifications_through_it_and_one_who_did_not_does_not()
    {
        var admin = await factory.SignInAsync("oc.admin3@example.com", "Admin");
        var wants = await factory.SignInAsync("oc.wants@example.com");
        await factory.SignInAsync("oc.silent@example.com");
        var wantsId = await IdOfAsync(admin, "oc.wants@example.com");
        var silentId = await IdOfAsync(admin, "oc.silent@example.com");
        await wants.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "General", inApp = true, email = false, channels = new[] { new { name = "Chat", displayName = "Chat", enabled = true } } } } });

        using (var scope = factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>();
            await publisher.PublishAsync(wantsId, "Your export is ready", "It is waiting for you.");
            await publisher.PublishAsync(silentId, "Your export is ready", "It is waiting for you.");
        }

        var delivered = await WaitForDeliveryAsync(factory, wantsId, "Chat", d => d.Status != DeliveryStatus.Pending);
        delivered!.Status.Should().Be(DeliveryStatus.Sent);
        lock (FakeChatChannel.Sent) FakeChatChannel.Sent.Should().Contain(s => s.Title == "Your export is ready" && s.Address.StartsWith("+1555"));
        await Task.Delay(1500);
        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<NotificationDelivery>().AnyAsync(d => d.UserId == silentId && d.ChannelName == "Chat"))
            .Should().BeFalse("Chat is off by default, so nothing is queued for someone who never chose it");
    }

    [Fact]
    public async Task The_delivery_mode_applies_to_the_new_channel_without_it_writing_any_code_for_it()
    {
        var admin = await factory.SignInAsync("oc.admin4@example.com", "Admin");
        var user = await factory.SignInAsync("oc.dev@example.com");
        var userId = await IdOfAsync(admin, "oc.dev@example.com");
        await user.PostAsync("/api/notifications/subscriptions/update", new { items = new[] { new { type = "Account", inApp = true, email = false, channels = new[] { new { name = "Chat", displayName = "Chat", enabled = true } } } } });
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Dev" } } });
        int before;
        lock (FakeChatChannel.Sent) before = FakeChatChannel.Sent.Count;
        try
        {
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>().PublishAsync(userId, "Dev mode check", "Nothing should be sent.", NotificationTypes.Account);

            var delivery = await WaitForDeliveryAsync(factory, userId, "Chat", d => d.Status != DeliveryStatus.Pending);

            delivery!.Status.Should().Be(DeliveryStatus.Suppressed);
            lock (FakeChatChannel.Sent) FakeChatChannel.Sent.Count.Should().Be(before);
        }
        finally
        {
            await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Prod" } } });
        }
    }

    [Fact]
    public async Task Email_is_just_another_channel_and_still_works_through_the_same_worker()
    {
        var admin = await factory.SignInAsync("oc.admin5@example.com", "Admin");
        await factory.SignInAsync("oc.mail@example.com");
        var userId = await IdOfAsync(admin, "oc.mail@example.com");
        await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Dev" } } });
        try
        {
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>().PublishAsync(userId, "Mail check", "Body.");

            // No mail server is configured on this host, so a Dev-mode delivery fails at the channel before anything is sent; what matters is that it is queued under the Email channel.
            var delivery = await WaitForDeliveryAsync(factory, userId, EmailOutboundChannel.ChannelName, d => d.Status != DeliveryStatus.Pending || d.Attempts > 0);
            delivery.Should().NotBeNull();
            delivery!.Channel.Should().Be(NotificationChannel.Email);
        }
        finally
        {
            await admin.PostAsync("/api/settings/update", new { items = new[] { new { name = "Notifications.DeliveryMode", value = "Prod" } } });
        }
    }
}
