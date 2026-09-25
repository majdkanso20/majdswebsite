using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.SharedKernel.Localization;

public static class HttpContextLocalization
{
    /// <summary>The language this request is being answered in (from Accept-Language, the user's saved language, or the app default),
    /// as settled by the request-localization middleware. English when that has not run.</summary>
    public static string Culture(this HttpContext context) =>
        context.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.Name ?? "en";

    /// <summary>Translates server text into the request's language; unchanged when there is no translation.</summary>
    public static string Localize(this HttpContext context, string text) =>
        context.RequestServices.GetService<IMessageCatalog>()?.Translate(text, context.Culture()) ?? text;
}
