using FluentValidation;
using MajdsApp.SharedKernel.Api;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Middleware;

/// <summary>
/// Catches every unhandled exception and converts it into a <see cref="ResponseDto{T}"/> (FR-ERR-001/002)
/// — no stack traces reach the client; full detail (with correlation id, via <see cref="CorrelationIdMiddleware"/>'s
/// log-context enrichment) is logged server-side only. The message and each error are translated into the request's language
/// (F-Localization FR-I18N-002) using the culture the request-localization middleware settled on.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            // FluentValidation's ValidationException(string) constructor (used for ad-hoc business-rule
            // checks in handlers, as opposed to the property-validator-driven ValidationBehavior path)
            // leaves Errors empty and puts the text only in Message — fall back to that so it isn't lost.
            var errors = ex.Errors.Any() ? ex.Errors.Select(e => e.ErrorMessage) : [ex.Message];
            await WriteAsync(context, ResponseStatusCode.ValidationError, "Validation failed.", errors);
        }
        catch (UnauthorizedAppException ex)
        {
            await WriteAsync(context, ResponseStatusCode.Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteAsync(context, ResponseStatusCode.Forbidden, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, ResponseStatusCode.NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteAsync(context, ResponseStatusCode.Conflict, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, ResponseStatusCode.Error, "An unexpected error occurred.");
        }
    }

    private static Task WriteAsync(HttpContext context, ResponseStatusCode code, string message, IEnumerable<string>? errors = null)
    {
        // An exception thrown before the request-localization middleware ran simply gets English.
        string Localize(string text) => context.Localize(text);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        var response = ResponseDto.Fail<object>(code, Localize(message), errors?.Select(Localize).ToList());
        return context.Response.WriteAsJsonAsync(response);
    }
}
