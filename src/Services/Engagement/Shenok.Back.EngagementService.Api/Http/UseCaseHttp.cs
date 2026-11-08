using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.EngagementService.Api.Http;

/// <summary>
/// Runs a use case after the route id and the header user id have been copied into its request.
/// The shared validation filter still validates an action argument that is itself a request.
/// Body DTOs are not requests, so this helper validates the composed request.
/// </summary>
internal static class UseCaseHttp
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

    public static async Task<IActionResult?> ProblemAsync<TRequest>(
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
