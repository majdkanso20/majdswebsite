using System.Diagnostics;
using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Records refused and failed actions (FR-AUDIT-001/006). Sits outermost — before validation and
/// authorization — so it sees permission denials (which never reach <see cref="AuditBehavior{TRequest,TResponse}"/>)
/// and failures that roll a transaction back. Every refused request is recorded (for security
/// monitoring, queries included); other failures are recorded for auditable commands. The write goes
/// through a separate context, and a failure to audit never masks the original error.
/// </summary>
public class FailureAuditBehavior<TRequest, TResponse>(
    IAuditLogWriter auditLogWriter, ICurrentUser currentUser, IHttpContextAccessor httpContextAccessor,
    ILogger<FailureAuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return await next(ct);
        }
        catch (Exception ex) when (ShouldRecord(request, ex))
        {
            try
            {
                var record = AuditRecordFactory.Create(
                    typeof(TRequest).Name, request, currentUser, httpContextAccessor.HttpContext,
                    timer.ElapsedMilliseconds, [], ex);
                await auditLogWriter.WriteFailureAsync(record, CancellationToken.None);
                if (httpContextAccessor.HttpContext is { } http) http.Items[Middleware.RefusedRequestAuditMiddleware.AlreadyRecordedKey] = true; // so the middleware does not record it twice
            }
            catch (Exception auditError)
            {
                logger.LogError(auditError, "Could not record the failed {RequestName} in the audit log", typeof(TRequest).Name);
            }

            throw;
        }
    }

    private static bool ShouldRecord(TRequest request, Exception ex) => ex switch
    {
        OperationCanceledException => false,
        ForbiddenException or UnauthorizedAppException => true,
        _ => request is IAuditableCommand
    };
}
