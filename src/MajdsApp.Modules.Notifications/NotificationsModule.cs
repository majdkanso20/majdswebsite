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
        services.AddScoped<IUserNotificationPublisher, NotificationPublisher>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<INotificationTemplate, SecurityEmailTemplate>();
        services.AddScoped<MajdsApp.SharedKernel.Dashboard.IDashboardWidget, UnreadNotificationsWidget>();
        services.AddSignalR();
        services.AddHostedService<NotificationDeliveryWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapHub<NotificationHub>("/hubs/notifications");
}
