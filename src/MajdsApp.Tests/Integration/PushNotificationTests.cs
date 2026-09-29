using System.Net;
using FluentAssertions;
using MajdsApp.Data;
using MajdsApp.Modules.Notifications;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Notifications FR-NOTIF-002/009: push as a real, pluggable channel (no third-party account — a
/// self-generated VAPID key pair signs every message), the same shape email and the test-only Sms channel use.</summary>
public class PushNotificationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private record VapidKeys(string PublicKey, string PrivateKey);

    [Fact]
    public async Task With_no_keys_set_up_push_has_no_public_key_and_is_left_out_of_a_user_s_preferences()
    {
        // Its own factory, not the class's shared one: another test here saves real keys, and settings persist for the fixture's lifetime.
        using var freshFactory = new ApiFactory();
        var user = await freshFactory.SignInAsync("push.user1@example.com");

        (await user.GetAsync<string?>("/api/notifications/push/vapid-public-key")).Data.Should().BeNull();
    }

    [Fact]
    public async Task Generating_a_key_pair_needs_the_send_permission_and_gives_back_a_real_usable_pair_each_time()
    {
        var admin = await factory.SignInAsync("push.admin1@example.com", "Admin");
        var plain = await factory.SignInAsync("push.plain1@example.com");

        (await plain.GetAsync<VapidKeys>("/api/notifications/push/generate-vapid-keys")).Status.Should().Be(HttpStatusCode.Forbidden);

        var first = (await admin.GetAsync<VapidKeys>("/api/notifications/push/generate-vapid-keys")).Data!;
        var second = (await admin.GetAsync<VapidKeys>("/api/notifications/push/generate-vapid-keys")).Data!;

        first.PublicKey.Should().NotBeNullOrWhiteSpace();
        first.PrivateKey.Should().NotBeNullOrWhiteSpace();
        first.PublicKey.Should().NotBe(second.PublicKey); // a fresh pair every time, not a fixed sample value
    }

    [Fact]
    public async Task Once_keys_are_saved_the_public_one_is_handed_to_a_subscribing_browser()
    {
        var admin = await factory.SignInAsync("push.admin2@example.com", "Admin");
        var keys = (await admin.GetAsync<VapidKeys>("/api/notifications/push/generate-vapid-keys")).Data!;

        await admin.PostAsync("/api/settings/update", new
        {
            items = new[]
            {
                new { name = "Notifications.Push.VapidPublicKey", value = keys.PublicKey },
                new { name = "Notifications.Push.VapidPrivateKey", value = keys.PrivateKey },
                new { name = "Notifications.Push.VapidSubject", value = "mailto:admin@example.com" }
            }
        });

        (await admin.GetAsync<string?>("/api/notifications/push/vapid-public-key")).Data.Should().Be(keys.PublicKey);
    }

    [Fact]
    public async Task Subscribing_stores_it_re_subscribing_the_same_endpoint_updates_it_and_unsubscribing_removes_it()
    {
        var admin = await factory.SignInAsync("push.admin3@example.com", "Admin");
        var user = await factory.SignInAsync("push.user3@example.com");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=push.user3")).Data!.Items.Single().Id;

        var subscribed = await user.PostAsync("/api/notifications/push/subscribe",
            new { endpoint = "https://push.example.com/abc", p256dh = "key-1", auth = "auth-1" });
        subscribed.Status.Should().Be(HttpStatusCode.OK);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<PushDeviceSubscription>().SingleAsync(s => s.UserId == userId)).P256dh.Should().Be("key-1");
        }

        // The same endpoint again (the browser re-subscribing with rotated keys) updates the row rather than adding a second one.
        await user.PostAsync("/api/notifications/push/subscribe", new { endpoint = "https://push.example.com/abc", p256dh = "key-2", auth = "auth-2" });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.Set<PushDeviceSubscription>().Where(s => s.UserId == userId).ToListAsync();
            rows.Should().ContainSingle().Which.P256dh.Should().Be("key-2");
        }

        await user.PostAsync("/api/notifications/push/unsubscribe", new { endpoint = "https://push.example.com/abc" });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<PushDeviceSubscription>().AnyAsync(s => s.UserId == userId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task A_user_with_no_subscription_resolves_to_no_address_so_the_channel_is_skipped()
    {
        var admin = await factory.SignInAsync("push.admin4@example.com", "Admin");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=push.admin4")).Data!.Items.Single().Id;

        using var scope = factory.Services.CreateScope();
        var channel = scope.ServiceProvider.GetServices<IOutboundChannel>().Single(c => c.Name == "Push");

        (await channel.ResolveAddressAsync(userId)).Should().BeNull();
    }

    [Fact]
    public async Task Two_devices_both_resolve_and_sending_without_keys_configured_is_refused_clearly()
    {
        // Its own factory: another test here saves real keys on the class's shared one, and settings persist for the fixture's lifetime.
        using var freshFactory = new ApiFactory();
        var admin = await freshFactory.SignInAsync("push.admin5@example.com", "Admin");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=push.admin5")).Data!.Items.Single().Id;
        await admin.PostAsync("/api/notifications/push/subscribe", new { endpoint = "https://push.example.com/one", p256dh = "k1", auth = "a1" });
        await admin.PostAsync("/api/notifications/push/subscribe", new { endpoint = "https://push.example.com/two", p256dh = "k2", auth = "a2" });

        using var scope = freshFactory.Services.CreateScope();
        var channel = scope.ServiceProvider.GetServices<IOutboundChannel>().Single(c => c.Name == "Push");

        var address = await channel.ResolveAddressAsync(userId);
        address.Should().Contain("push.example.com/one").And.Contain("push.example.com/two"); // both devices, one address string (a JSON array)

        // No VAPID keys saved on this factory: a clear configuration error, not a silent no-op or an obscure crypto exception.
        var sending = async () => await channel.SendAsync(address!, new OutboundMessage(userId, "General", "Title", "Message", null));
        (await sending.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("VAPID");
    }
}
