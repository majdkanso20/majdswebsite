using MajdsApp.SharedKernel.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Global gate enforced on every request (P5 FR-PLUG-033/037): if the action belongs to a plugin
/// assembly (by naming convention, <c>MajdsApp.Plugins.*</c>) that is currently disabled, the request
/// is rejected as <c>Forbidden</c> before the action runs — the same immediate, no-restart-needed
/// enforcement a permission check gets, applied at the whole-plugin granularity.
/// </summary>
public class PluginGateFilter(IPluginStateCache pluginState) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var assemblyName = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerTypeInfo.Assembly.GetName().Name;

        if (assemblyName is not null
            && assemblyName.StartsWith("MajdsApp.Plugins.", StringComparison.Ordinal)
            && !pluginState.IsEnabled(assemblyName))
        {
            context.Result = new ObjectResult(ResponseDto.Fail<object>(ResponseStatusCode.Forbidden, "This plugin is currently disabled."))
            {
                StatusCode = (int)ResponseStatusCode.Forbidden
            };
            return;
        }

        await next();
    }
}
