using FluentAssertions;
using MajdsApp.Modules.Notifications;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MajdsApp.Tests.Integration;

/// <summary>F-Notifications FR-NOTIF-001/004: the single dispatch entry point the SRS names, and severity/payload persisted with the notification.</summary>
public class NotificationDispatcherTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task SendAsync_and_PublishAsync_are_the_same_dispatcher_and_both_reach_the_recipient()
    {
        var admin = await factory.SignInAsync("nd.admin1@example.com", "Admin");
        var user = await factory.SignInAsync("nd.user1@example.com");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=nd.user1")).Data!.Items.Single().Id;

        using (var scope = factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
            var publisher = scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>();

            // Both interfaces are implemented by the same class (FR-NOTIF-001): the low-level call the SRS names...
            await dispatcher.SendAsync(new NotificationMessage("Dispatcher says hi", "sent through SendAsync", Severity: NotificationSeverity.Warning), [userId]);
            // ...and the convenience wrapper most callers use.
            await publisher.PublishAsync(userId, "Publisher says hi", "sent through PublishAsync", severity: NotificationSeverity.Success, payload: "{\"exportId\":7}");
        }

        var list = (await user.GetAsync<PagedData<NotificationDto>>("/api/notifications/list?page=1&pageSize=10")).Data!.Items;

        var viaDispatcher = list.Single(n => n.Title == "Dispatcher says hi");
        viaDispatcher.Severity.Should().Be("Warning");

        var viaPublisher = list.Single(n => n.Title == "Publisher says hi");
        viaPublisher.Severity.Should().Be("Success");
        viaPublisher.Payload.Should().Be("{\"exportId\":7}");
    }

    [Fact]
    public async Task A_notification_with_no_severity_or_payload_defaults_to_Info_and_null()
    {
        var admin = await factory.SignInAsync("nd.admin2@example.com", "Admin");
        var user = await factory.SignInAsync("nd.user2@example.com");
        var userId = (await admin.GetAsync<PagedData<UserRow>>("/api/users/list?page=1&pageSize=5&filter=nd.user2")).Data!.Items.Single().Id;

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>().PublishAsync(userId, "Plain", "no severity given");

        var found = (await user.GetAsync<PagedData<NotificationDto>>("/api/notifications/list?page=1&pageSize=10")).Data!.Items.Single(n => n.Title == "Plain");
        found.Severity.Should().Be("Info");
        found.Payload.Should().BeNull();
    }
}
