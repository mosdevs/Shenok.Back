using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shenok.Back.Api.Shared.Correlation;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationId.HeaderName, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[CorrelationId.ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationId.HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            await next(context);
    }
}
