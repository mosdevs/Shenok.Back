using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Shenok.Back.Api.Shared.Security;

/// <summary>
/// Records that the selected endpoint is an internal service call.
/// A header or a path prefix cannot set the same flag.
/// </summary>
public sealed class InternalServiceScopeFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (IsServiceEndpoint(context))
            context.HttpContext.Items[InternalCaller.ServiceItemKey] = true;

        return next();
    }

    private static bool IsServiceEndpoint(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<InternalServiceAttribute>().Any())
            return true;

        if (context.ActionDescriptor is not ControllerActionDescriptor action)
            return false;

        return action.MethodInfo.IsDefined(typeof(InternalServiceAttribute), inherit: false)
            || action.ControllerTypeInfo.IsDefined(typeof(InternalServiceAttribute), inherit: false);
    }
}
