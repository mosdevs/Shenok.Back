using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.Gateway.Api.Http;

/// <summary>
/// Validates the composed request, then runs the use case.
/// The shared filter cannot see a request built from the route and the token.
/// </summary>
internal static class GatewayHttp
{
    public static async Task<IActionResult> OkAsync<TRequest, TResponse>(
        HttpContext http,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> execute,
        CancellationToken cancellationToken)
    {
        var invalid = await ProblemAsync(http, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        return new OkObjectResult(await execute(request, cancellationToken));
    }

    public static async Task<IActionResult> OkAsync<TRequest>(
        HttpContext http,
        TRequest request,
        Func<TRequest, CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        var invalid = await ProblemAsync(http, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        await execute(request, cancellationToken);
        return new OkResult();
    }

    public static IActionResult FieldProblem(HttpContext http, string field, string message) =>
        ApiProblems.Result(ApiProblems.Validation(
            http.TraceIdentifier,
            new Dictionary<string, string[]> { [field] = [message] }));

    private static async Task<IActionResult?> ProblemAsync<TRequest>(
        HttpContext http,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (http.RequestServices.GetService<IValidator<TRequest>>() is not IValidator<TRequest> validator)
            return null;

        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
            return null;

        return ApiProblems.Result(ApiProblems.Validation(http.TraceIdentifier, ApiProblems.Group(result.Errors)));
    }
}
