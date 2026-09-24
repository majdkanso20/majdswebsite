using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MajdsApp.SharedKernel.Api;

/// <summary>
/// Maps <see cref="ResponseDto{T}.Code"/> to the matching HTTP status code (FR-API-006) while the
/// envelope itself stays the response body. Registered globally so no action sets its own status code.
/// </summary>
public class ResponseStatusCodeFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IResponseDto response } objectResult)
        {
            objectResult.StatusCode = (int)response.Code;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
