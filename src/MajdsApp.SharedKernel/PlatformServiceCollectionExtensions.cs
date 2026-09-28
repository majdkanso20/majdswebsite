using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Mapping;
using System.Reflection;
using FluentValidation;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Caching;
using MajdsApp.SharedKernel.Features;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Notifications;
using MajdsApp.SharedKernel.Plugins;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MajdsApp.SharedKernel;

/// <summary>
/// Wires up P1–P4 in one call so a host project stays a thin composition root (cross-cutting standard #2).
/// </summary>
public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared kernel (current-user accessor, permission-checker default, MediatR +
    /// pipeline behaviors in the mandated order, FluentValidation) and discovers every
    /// <see cref="IFeatureModule"/> in <paramref name="moduleAssemblies"/> (P2).
    /// </summary>
    public static IServiceCollection AddPlatformCore(
        this IServiceCollection services, IConfiguration configuration, params Assembly[] moduleAssemblies)
    {
        services.AddHttpContextAccessor();
        services.AddPlatformCaching(configuration);
        services.AddPlatformMapping(moduleAssemblies);
        services.AddModuleOptions<MajdsApp.SharedKernel.Paging.PagingOptions>(configuration, "Paging");
        MajdsApp.SharedKernel.Paging.PagedRequest.MaxPageSize = configuration.GetSection("Paging").Get<MajdsApp.SharedKernel.Paging.PagingOptions>()?.MaxPageSize is { } max and >= 1 ? max : 100;
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.TryAddScoped<IPermissionChecker, AllowAllPermissionChecker>();
        services.TryAddScoped<ISettingsProvider, DefaultSettingsProvider>();
        services.TryAddScoped<IAuditLogWriter, NullAuditLogWriter>();
        services.TryAddScoped<IAuditChangeBuffer, AuditChangeBuffer>();
        services.TryAddScoped<IUserNotificationPublisher, NullNotificationPublisher>();
        services.TryAddScoped<MajdsApp.SharedKernel.Jobs.IBackgroundJobQueue, MajdsApp.SharedKernel.Jobs.NullBackgroundJobQueue>();
        services.TryAddSingleton<MajdsApp.SharedKernel.Localization.IMessageCatalog, MajdsApp.SharedKernel.Localization.NullMessageCatalog>();
        services.TryAddScoped<IFeatureChecker, DefaultFeatureChecker>();
        services.TryAddScoped<MajdsApp.SharedKernel.Notifications.IDeliveryModePolicy, MajdsApp.SharedKernel.Notifications.SettingsDeliveryModePolicy>();
        services.TryAddSingleton<IPluginStateCache, AllowAllPluginStateCache>();
        services.AddMemoryCache();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(moduleAssemblies);
            cfg.LicenseKey = configuration["MediatR:LicenseKey"]; // required for production use; unset is fine in development
        });

        // Pipeline order per P4 Agent Notes: Logging -> Performance -> Validation -> Authorization -> Caching -> Transaction.
        // Audit runs innermost (after Transaction) so its write joins the same commit as the change it
        // describes (F-Audit) — never persisted if the handler throws and the transaction rolls back.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FailureAuditBehavior<,>)); // outermost after timing, so it sees denials and rollbacks
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FeatureBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        services.AddValidatorsFromAssemblies(moduleAssemblies);

        services.AddFeatureModules(configuration, moduleAssemblies);

        return services;
    }

    /// <summary>
    /// Adds controllers with the platform's global result filter (FR-API-006) and a
    /// <see cref="ResponseDto{T}"/>-shaped validation-failure response (FR-API-007), then registers
    /// every module assembly as an application part so their controllers are discovered (FR-MOD-005).
    /// </summary>
    public static IMvcBuilder AddPlatformControllers(this IServiceCollection services, params Assembly[] moduleAssemblies)
    {
        var builder = services.AddControllers(options =>
        {
            options.Filters.Add<ResponseStatusCodeFilter>();
            options.Filters.Add<PluginGateFilter>();
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors.Select(e => $"{entry.Key}: {e.ErrorMessage}"));

                var response = ResponseDto.Fail<object>(ResponseStatusCode.ValidationError, context.HttpContext.Localize("Validation failed."), errors);
                return new BadRequestObjectResult(response) { StatusCode = (int)ResponseStatusCode.ValidationError };
            };
        });

        return builder.AddFeatureModuleControllers(moduleAssemblies);
    }
}
