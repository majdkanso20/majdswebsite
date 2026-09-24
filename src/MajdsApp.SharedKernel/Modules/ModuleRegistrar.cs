using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace MajdsApp.SharedKernel.Modules;

/// <summary>
/// Host-side discovery for feature modules (P2). The host passes the module assemblies explicitly —
/// deterministic and fast — rather than scanning every loaded assembly in the process.
/// </summary>
public static class ModuleRegistrar
{
    /// <summary>
    /// Instantiates and invokes every <see cref="IFeatureModule"/> found in <paramref name="moduleAssemblies"/>,
    /// then convention-registers every class implementing <see cref="IScopedService"/>, <see cref="ITransientService"/>,
    /// or <see cref="ISingletonService"/> (FR-MOD-001..003).
    /// </summary>
    public static IServiceCollection AddFeatureModules(
        this IServiceCollection services, IConfiguration configuration, params Assembly[] moduleAssemblies)
    {
        foreach (var moduleType in moduleAssemblies
                     .SelectMany(a => a.GetTypes())
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IFeatureModule).IsAssignableFrom(t)))
        {
            var module = (IFeatureModule)Activator.CreateInstance(moduleType)!;
            module.ConfigureServices(services, configuration);
        }

        // Recurring jobs are many implementations of one interface, so they must be *appended*. The
        // convention scan below skips a service type once it's registered and would keep only the
        // first job; running this scan first means that one skips IRecurringJob and just adds the rest.
        services.Scan(scan => scan.FromAssemblies(moduleAssemblies)
            .AddClasses(classes => classes.AssignableTo<Jobs.IRecurringJob>())
            .As<Jobs.IRecurringJob>()
            .WithScopedLifetime());

        services.Scan(scan => scan.FromAssemblies(moduleAssemblies)
            .AddClasses(classes => classes.AssignableTo<Search.ISearchProvider>())
            .As<Search.ISearchProvider>()
            .WithScopedLifetime());

        services.Scan(scan => scan.FromAssemblies(moduleAssemblies)
            .AddClasses(classes => classes.AssignableTo<IScopedService>())
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithScopedLifetime()

            .AddClasses(classes => classes.AssignableTo<ITransientService>())
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithTransientLifetime()

            .AddClasses(classes => classes.AssignableTo<ISingletonService>())
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());

        return services;
    }

    /// <summary>Maps the optional endpoints of every discovered module (FR-MOD-001).</summary>
    public static IEndpointRouteBuilder MapFeatureModuleEndpoints(this IEndpointRouteBuilder endpoints, params Assembly[] moduleAssemblies)
    {
        foreach (var moduleType in moduleAssemblies
                     .SelectMany(a => a.GetTypes())
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IFeatureModule).IsAssignableFrom(t)))
        {
            var module = (IFeatureModule)Activator.CreateInstance(moduleType)!;
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    /// <summary>Registers each module assembly as an MVC application part so its controllers are discovered (FR-MOD-005).</summary>
    public static IMvcBuilder AddFeatureModuleControllers(this IMvcBuilder builder, params Assembly[] moduleAssemblies)
    {
        foreach (var assembly in moduleAssemblies)
            builder.AddApplicationPart(assembly);

        return builder;
    }
}
