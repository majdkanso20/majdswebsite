using System.Reflection;
using MajdsApp.SharedKernel.Behaviors;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MajdsApp.SharedKernel.Api;

/// <summary>
/// The declarative permission check for controller actions (F-Authorization FR-AUTHZ-004): put <see cref="RequiresPermissionAttribute"/> on an action, or on a whole controller,
/// and this filter runs before the action. It is the same rule the pipeline applies to a MediatR request, with the same answers: 401 when nobody is signed in, 403 with the
/// missing permission when they lack it, both as the standard <c>ResponseDto</c>. An action that just sends a request needs nothing here, since the request declares its own.
/// </summary>
public class PermissionActionFilter(ICurrentUser currentUser, IPermissionChecker permissionChecker) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var required = descriptor.MethodInfo.GetCustomAttributes<RequiresPermissionAttribute>(inherit: true)
                .Concat(descriptor.ControllerTypeInfo.GetCustomAttributes<RequiresPermissionAttribute>(inherit: true))
                .Select(a => a.Permission).Distinct().ToList();

            foreach (var permission in required)
            {
                if (!currentUser.IsAuthenticated)
                    throw new UnauthorizedAppException("Authentication is required.");

                if (!await permissionChecker.HasPermissionAsync(permission, context.HttpContext.RequestAborted))
                    throw new ForbiddenException($"Missing permission '{permission}'.");
            }
        }

        await next();
    }
}
