using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Mapping;

/// <summary>
/// The platform's one place for turning entities into DTOs (P4 FR-XC-007). Mapping is by name and constructor by default, so a DTO whose members
/// match its entity needs no code at all; anything else is described once, in a module's Mapster <see cref="IRegister"/> class, which is found by
/// scanning the module assemblies. A handler asks for <see cref="IObjectMapper"/> instead of writing the same <c>new XDto(a.Id, a.Name, ...)</c> again.
/// </summary>
public interface IObjectMapper
{
    /// <summary>Maps one object (an entity already in memory) to <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TDestination>(object source);

    /// <summary>The same mapping as an expression, for a database query's <c>Select</c>, so only the DTO's columns are read and the mapping runs in SQL.</summary>
    Expression<Func<TSource, TDestination>> Projection<TSource, TDestination>();
}

public sealed class ObjectMapper(TypeAdapterConfig config) : IObjectMapper
{
    private readonly ConcurrentDictionary<(Type, Type), object> _projections = new();

    public TDestination Map<TDestination>(object source) => source.Adapt<TDestination>(config);

    public Expression<Func<TSource, TDestination>> Projection<TSource, TDestination>() =>
        (Expression<Func<TSource, TDestination>>)_projections.GetOrAdd((typeof(TSource), typeof(TDestination)),
            _ => default(TSource)!.BuildAdapter(config).CreateProjectionExpression<TDestination>());
}

public static class MappingRegistration
{
    /// <summary>Registers <see cref="IObjectMapper"/> with every <see cref="IRegister"/> found in the module assemblies (one configuration for the whole application).</summary>
    public static IServiceCollection AddPlatformMapping(this IServiceCollection services, params Assembly[] moduleAssemblies)
    {
        var config = new TypeAdapterConfig();
        config.Scan(moduleAssemblies);
        services.AddSingleton(config);
        services.AddSingleton<IObjectMapper, ObjectMapper>();
        return services;
    }
}
