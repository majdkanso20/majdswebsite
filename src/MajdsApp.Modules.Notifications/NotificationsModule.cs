using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Notifications;

public class NotificationsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Both interfaces resolve to the same instance per scope (FR-NOTIF-001): SendAsync is the single dispatch entry point the SRS
        // names, PublishAsync/PublishToAllAsync/PublishToRoleAsync are the convenience wrapper most callers use instead.
        services.AddScoped<NotificationPublisher>();
        services.AddScoped<IUserNotificationPublisher>(sp => sp.GetRequiredService<NotificationPublisher>());
        services.AddScoped<INotificationDispatcher>(sp => sp.GetRequiredService<NotificationPublisher>());
        services.AddScoped<IOutboundChannel, EmailOutboundChannel>(); // the first channel; any module can add another the same way
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationTemplate, SecurityEmailTemplate>();
        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, UnreadNotificationsWidget>();
        services.AddSignalR();
        services.AddHostedService<NotificationDeliveryWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapHub<NotificationHub>("/hubs/notifications");
}
