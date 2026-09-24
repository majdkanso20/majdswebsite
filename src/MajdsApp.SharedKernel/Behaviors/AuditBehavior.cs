using System.Diagnostics;
using MajdsApp.SharedKernel.Audit;
using MajdsApp.SharedKernel.Data;
using MajdsApp.SharedKernel.Security;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Records every successful <see cref="IAuditableCommand"/> (F-Audit) with its request metadata,
/// redacted parameters and the entity changes it caused. Registered after TransactionBehavior
/// (innermost) so the audit write lands in the DbContext before that behavior's commit — atomic with
/// the change it describes, and never persisted if the handler throws and the transaction rolls back.
/// Pending handler changes are flushed first so the change buffer already holds them; a command that
/// isn't transactional (e.g. one backed by an ASP.NET Identity store, which self-saves) simply has
/// nothing left to flush. Failures are recorded by <see cref="FailureAuditBehavior{TRequest,TResponse}"/>.
/// </summary>
public class AuditBehavior<TRequest, TResponse>(
    IAuditLogWriter auditLogWriter, ICurrentUser currentUser, IAuditChangeBuffer changeBuffer,
    IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var response = await next(ct);
        timer.Stop();

        if (request is IAuditableCommand)
        {
            await unitOfWork.SaveChangesAsync(ct);
            var record = AuditRecordFactory.Create(
                typeof(TRequest).Name, request, currentUser, httpContextAccessor.HttpContext,
                timer.ElapsedMilliseconds, changeBuffer.Drain());
            await auditLogWriter.WriteAsync(record, ct);
        }

        return response;
    }
}
