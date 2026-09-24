using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using Microsoft.AspNetCore.Http;

namespace MajdsApp.SharedKernel.Audit;

/// <summary>Builds the <see cref="AuditRecord"/> shared by the success and failure paths, so what is
/// captured about a request (who, where from, how long, what parameters) is defined in one place.</summary>
public static class AuditRecordFactory
{
    public static AuditRecord Create(
        string action, object request, ICurrentUser user, HttpContext? http, long durationMs,
        IReadOnlyList<AuditChange> changes, Exception? failure = null)
    {
        var (succeeded, outcome) = failure is null ? (true, "Success") : (false, OutcomeFor(failure));

        return new AuditRecord(
            action, user.UserId, user.UserName,
            http?.Request.Method, http?.Request.Path.Value,
            (int)Math.Min(durationMs, int.MaxValue),
            http?.Connection.RemoteIpAddress?.ToString(),
            Truncate(http?.Request.Headers.UserAgent.ToString(), 300),
            succeeded, outcome,
            Truncate(failure?.Message, 500),
            AuditRedactor.SerializeParameters(request),
            changes);
    }

    public static string OutcomeFor(Exception ex) => ex switch
    {
        ForbiddenException => "Forbidden",
        UnauthorizedAppException => "Unauthorized",
        FluentValidation.ValidationException => "Invalid",
        NotFoundException => "NotFound",
        ConflictException => "Conflict",
        _ => "Error"
    };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
