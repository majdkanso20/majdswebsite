using Microsoft.AspNetCore.Mvc;

namespace MajdsApp.SharedKernel.Api;

/// <summary>
/// Base controller for all feature-module controllers (P1). Every action must return
/// <see cref="ResponseDto{T}"/> built through these helpers so the envelope is never
/// constructed by hand in a handler/controller (DRY, FR-API-005).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ResponseDto<T> Ok<T>(T data, string? message = null) => ResponseDto.Ok(data, message);

    protected ResponseDto<T> Fail<T>(ResponseStatusCode code, string? message = null, IEnumerable<string>? errors = null) =>
        ResponseDto.Fail<T>(code, message, errors);
}
