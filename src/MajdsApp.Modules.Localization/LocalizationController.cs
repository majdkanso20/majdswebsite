using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Localization;

public record LanguageDto(string Code, string Label, bool Rtl, bool IsDefault);

public record ResourcesDto(string Culture, IReadOnlyDictionary<string, string> Messages);

public class LocalizationController(IMessageCatalog catalog) : ApiControllerBase
{
    /// <summary>The languages that can be chosen (FR-I18N-003), the default first.</summary>
    [Authorize]
    [HttpGet("languages")]
    public ResponseDto<IReadOnlyList<LanguageDto>> Languages() =>
        Ok<IReadOnlyList<LanguageDto>>(catalog.Languages.Select((l, i) => new LanguageDto(l.Code, l.Label, l.Rtl, i == 0)).ToList());

    /// <summary>The server's translations for one culture, keyed by the English text (or template). Anonymous, so a client can load
    /// them before anyone has signed in. English needs none and returns an empty set.</summary>
    [AllowAnonymous]
    [HttpGet("resources")]
    public ResponseDto<ResourcesDto> Resources([FromQuery] string? culture)
    {
        var requested = string.IsNullOrWhiteSpace(culture) ? "en" : culture.Trim();
        return Ok(new ResourcesDto(requested, catalog.GetResources(requested)));
    }
}
