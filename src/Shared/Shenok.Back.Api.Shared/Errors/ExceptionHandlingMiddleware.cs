using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.Api.Shared.Errors;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (status, title, type) = Map(exception);
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogInformation(exception, "Request failed with {Status}", status);

        var problem = new ApiProblem(
            type,
            title,
            status,
            exception.Message,
            context.TraceIdentifier,
            null);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static (int Status, string Title, string Type) Map(Exception exception) => exception switch
    {
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://shenok.dev/problems/unauthorized"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", "https://shenok.dev/problems/forbidden"),
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found", "https://shenok.dev/problems/not-found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict", "https://shenok.dev/problems/conflict"),
        DomainException => (StatusCodes.Status400BadRequest, "Domain rule violated", "https://shenok.dev/problems/domain"),
        _ => (StatusCodes.Status500InternalServerError, "Server error", "https://shenok.dev/problems/server")
    };
}
