using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MajdsApp.SharedKernel.Configuration;

/// <summary>
/// How a module gets its own configuration (P2 FR-MOD-004): a class of settings bound from one named section through the options pattern
/// (<c>IOptions&lt;T&gt;</c>), checked with data annotations (and <see cref="System.ComponentModel.DataAnnotations.IValidatableObject"/> for rules
/// that involve several values) <b>when the application starts</b>. A wrong value stops the start with a message naming the setting, instead of
/// surfacing as a strange failure on the first request that happens to use it.
/// </summary>
public static class ModuleOptions
{
    public static OptionsBuilder<T> AddModuleOptions<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class =>
        services.AddOptions<T>()
            .Bind(configuration.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
