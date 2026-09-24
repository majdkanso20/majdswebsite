using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MediatR;

namespace MajdsApp.SharedKernel.Behaviors;

/// <summary>
/// Enforces <see cref="RequiresPermissionAttribute"/> on the request type before the handler runs
/// (FR-XC-001/002, F-Authorization). Throws so business logic never checks role names itself.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse>(ICurrentUser currentUser, IPermissionChecker permissionChecker)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var attribute = typeof(TRequest).GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: true)
            .OfType<RequiresPermissionAttribute>()
            .FirstOrDefault();

        if (attribute is not null)
        {
            if (!currentUser.IsAuthenticated)
                throw new UnauthorizedAppException("Authentication is required.");

            if (!await permissionChecker.HasPermissionAsync(attribute.Permission, ct))
                throw new ForbiddenException($"Missing permission '{attribute.Permission}'.");
        }

        return await next(ct);
    }
}
