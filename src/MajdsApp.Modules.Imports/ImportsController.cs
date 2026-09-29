using MajdsApp.SharedKernel.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Imports;

[Authorize]
public class ImportsController(IMediator mediator) : ApiControllerBase
{
    /// <summary>Queues a background import and returns the job straight away; the user is notified when it is done.</summary>
    [HttpPost("start")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(MajdsApp.SharedKernel.Security.RateLimitPolicies.Expensive)]
    public async Task<ResponseDto<ImportJobDto>> Start([FromQuery] string source, IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new FluentValidation.ValidationException("Choose a file to import.");
        if (file.Length > MajdsApp.SharedKernel.Import.TabularReader.MaxBytes)
            throw new FluentValidation.ValidationException($"The file must be no larger than {MajdsApp.SharedKernel.Import.TabularReader.MaxBytes / (1024 * 1024)} MB.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        return Ok(await mediator.Send(new StartImportCommand(source, buffer.ToArray(), file.FileName)));
    }

    /// <summary>The caller's recent imports with their status.</summary>
    [HttpGet("list")]
    public async Task<ResponseDto<IReadOnlyList<ImportJobDto>>> List() =>
        Ok(await mediator.Send(new ListMyImportsQuery()));

    /// <summary>The blank template for one import source.</summary>
    [HttpGet("template")]
    public async Task<IActionResult> Template([FromQuery] string source, [FromQuery] string? format)
    {
        var file = await mediator.Send(new GetImportTemplateQuery(source, format));
        return File(file.Content, file.ContentType, file.FileName);
    }
}
