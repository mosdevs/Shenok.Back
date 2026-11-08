using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.Api.Shared.Security;

public sealed class InternalApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health")
            || context.Request.Path.StartsWithSegments("/ready")
            || context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        var expected = configuration[InternalCaller.ApiKeyConfigurationPath];
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");

        if (!context.Request.Headers.TryGetValue(InternalCaller.ApiKeyHeader, out var supplied)
            || !KeysMatch(expected, supplied.ToString()))
        {
            await ApiProblems.WriteAsync(context, new ApiProblem(
                "https://shenok.dev/problems/unauthorized",
                "Unauthorized",
                StatusCodes.Status401Unauthorized,
                "The internal API key is missing.",
                context.TraceIdentifier,
                null));
            return;
        }

        await next(context);
    }

    private static bool KeysMatch(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
