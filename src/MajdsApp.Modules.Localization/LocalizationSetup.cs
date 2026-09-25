using System.Globalization;
using MajdsApp.SharedKernel.Localization;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Localization;

public class LocalizationModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MessageCatalog>();
        services.AddSingleton<IMessageCatalog>(sp => sp.GetRequiredService<MessageCatalog>());
    }
}

/// <summary>
/// Decides which language a request is answered in (F-Localization FR-I18N-002/005), in this order:
/// 1. the languages the client asks for in <c>Accept-Language</c> (the Angular app sends the one the user picked), by preference;
/// 2. the signed-in user's saved language (<c>General.DefaultLanguage</c>, which a user may override for themselves);
/// 3. the application's default language, and finally English.
/// The result sets the request's culture, which also drives FluentValidation's own (already translated) messages.
/// </summary>
public class PlatformCultureProvider : RequestCultureProvider
{
    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        foreach (var candidate in httpContext.Request.GetTypedHeaders().AcceptLanguage.OrderByDescending(l => l.Quality ?? 1))
        {
            if (candidate.Value.HasValue && MessageCatalog.Supported.Any(l => l.Code.Equals(BaseOf(candidate.Value.Value), StringComparison.OrdinalIgnoreCase)))
                return new ProviderCultureResult(BaseOf(candidate.Value.Value));
        }

        var settings = httpContext.RequestServices.GetRequiredService<ISettingsProvider>();
        var userId = httpContext.RequestServices.GetRequiredService<ICurrentUser>().UserId;
        var values = await settings.GetAllForUserAsync(userId);
        var saved = values.GetValueOrDefault("General.DefaultLanguage");

        return MessageCatalog.Supported.Any(l => l.Code.Equals(saved, StringComparison.OrdinalIgnoreCase))
            ? new ProviderCultureResult(saved!.ToLowerInvariant())
            : null; // falls through to the default request culture, English
    }

    private static string BaseOf(string tag) => tag.Split('-', '_')[0];
}

public static class LocalizationApplicationExtensions
{
    /// <summary>Sets each request's culture. Put it after authentication, so a signed-in user's saved language is known.</summary>
    public static IApplicationBuilder UsePlatformLocalization(this IApplicationBuilder app)
    {
        var supported = MessageCatalog.Supported.Select(l => new CultureInfo(l.Code)).ToList();
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("en"),
            SupportedCultures = supported,
            SupportedUICultures = supported,
            ApplyCurrentCultureToResponseHeaders = true
        };
        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new PlatformCultureProvider());

        return app.UseRequestLocalization(options);
    }
}
