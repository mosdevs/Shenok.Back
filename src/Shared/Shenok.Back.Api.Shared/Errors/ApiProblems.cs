using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Shenok.Back.Api.Shared.Errors;

/// <summary>
/// One problem-details shape for validation and for middleware.
/// Unknown server failures use <see cref="ServerDetail"/> so an exception message cannot leave the process.
/// </summary>
public static class ApiProblems
{
    public const string ValidationType = "https://shenok.dev/problems/validation";

    public const string ServerDetail = "Server error";

    public const string ProblemContentType = "application/problem+json";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ApiProblem Validation(string traceId, IReadOnlyDictionary<string, string[]> errors) =>
        new(
            ValidationType,
            "Validation failed",
            StatusCodes.Status400BadRequest,
            "One or more fields are invalid.",
            traceId,
            errors);

    public static Dictionary<string, string[]> Group(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => string.IsNullOrEmpty(group.Key) ? "_" : group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

    public static Dictionary<string, string[]> FromModelState(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        return modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => string.IsNullOrEmpty(entry.Key) ? "_" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The request body is not valid JSON."
                        : error.ErrorMessage)
                    .Distinct()
                    .ToArray());
    }

    public static ObjectResult Result(ApiProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { ProblemContentType }
        };
    }

    public static Task WriteAsync(HttpContext http, ApiProblem problem)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(problem);
        http.Response.StatusCode = problem.Status;
        http.Response.ContentType = ProblemContentType;
        return http.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
