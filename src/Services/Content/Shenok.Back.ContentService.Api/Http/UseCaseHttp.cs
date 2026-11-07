using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.ContentService.Application.Counters;

namespace Shenok.Back.ContentService.Api.Http;

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

    public static async Task<IActionResult> CreatedAsync<TRequest, TResponse>(
        HttpContext http,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> execute,
        Func<TResponse, string> location,
        CancellationToken cancellationToken)
    {
        var invalid = await ProblemAsync(http, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        var response = await execute(request, cancellationToken);
        return new CreatedResult(location(response), response);
    }

    public static async Task<IActionResult> OkOrDeferredAsync<TRequest, TResponse>(
        HttpContext http,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> execute,
        CancellationToken cancellationToken)
    {
        var invalid = await ProblemAsync(http, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        try
        {
            return new OkObjectResult(await execute(request, cancellationToken));
        }
        catch (CounterUpdateDeferredException exception)
        {
            return new ObjectResult(new ApiProblem(
                "https://shenok.dev/problems/not-implemented",
                "Not Implemented",
                StatusCodes.Status501NotImplemented,
                exception.Message,
                http.TraceIdentifier,
                null))
            {
                StatusCode = StatusCodes.Status501NotImplemented,
                ContentTypes = { "application/problem+json" }
            };
        }
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

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => string.IsNullOrEmpty(group.Key) ? "_" : group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct().ToArray());

        return new ObjectResult(new ApiProblem(
            "https://shenok.dev/problems/validation",
            "Validation failed",
            StatusCodes.Status400BadRequest,
            "One or more fields are invalid.",
            http.TraceIdentifier,
            errors))
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }
}
