using FluentValidation;
using MediatR;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Runs every registered <see cref="IValidator{T}"/> for the request before the handler executes
/// (FR-XC-001/002). On failure it throws FluentValidation's <see cref="ValidationException"/>, which
/// the exception-handling middleware (F-Errors) maps to <c>ResponseDto</c> with Code=ValidationError.
/// Handlers never validate their own input.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
