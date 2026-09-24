using MajdsApp.SharedKernel.Data;
using MediatR;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Wraps any request implementing <see cref="ITransactionalCommand"/> in a unit-of-work transaction
/// (FR-XC-002/003): commits on success, rolls back on exception. Handlers never manage transactions.
/// </summary>
public class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ITransactionalCommand)
            return await next(ct);

        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var response = await next(ct);
            await unitOfWork.CommitAsync(ct);
            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
