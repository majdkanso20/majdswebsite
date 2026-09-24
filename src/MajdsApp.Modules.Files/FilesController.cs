using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Paging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.Modules.Files;

[Authorize]
public class FilesController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("list")]
    public async Task<ResponseDto<PagedResponse<FileDto>>> List([FromQuery] PagedRequest request) =>
        Ok(await mediator.Send(new ListFilesQuery(request)));

    [HttpPost("upload")]
    [RequestSizeLimit(200L * 1024 * 1024)] // hard transport ceiling; the real limit is the Files.MaxUploadMb setting
    public async Task<ResponseDto<FileDto>> Upload(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await mediator.Send(new UploadFileCommand(file.FileName, file.ContentType, file.Length, stream)));
    }

    /// <summary>Returns the raw file (not the ResponseDto envelope — it's a binary stream). Errors still
    /// come back as the envelope via the exception middleware.</summary>
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] Guid fileId)
    {
        var file = await mediator.Send(new DownloadFileQuery(fileId));
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("delete")]
    public async Task<ResponseDto<object?>> Delete([FromBody] DeleteFileCommand command)
    {
        await mediator.Send(command);
        return Ok<object?>(null);
    }
}
