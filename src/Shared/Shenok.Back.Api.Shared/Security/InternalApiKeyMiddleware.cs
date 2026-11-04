using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Shenok.Back.Api.Shared.Security;

public sealed class InternalApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        var expected = configuration[InternalCaller.ApiKeyConfigurationPath];
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");

        if (!context.Request.Headers.TryGetValue(InternalCaller.ApiKeyHeader, out var supplied)
            || !string.Equals(supplied.ToString(), expected, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://shenok.dev/problems/unauthorized",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "The internal API key is missing.",
                traceId = context.TraceIdentifier,
                errors = (object?)null
            });
            return;
        }

        await next(context);
    }
}
