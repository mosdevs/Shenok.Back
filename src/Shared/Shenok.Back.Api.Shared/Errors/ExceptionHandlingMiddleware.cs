using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.Api.Shared.Errors;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
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

        var detail = status >= StatusCodes.Status500InternalServerError && exception is not UnavailableException
            ? ApiProblems.ServerDetail
            : exception.Message;

        await ApiProblems.WriteAsync(context, new ApiProblem(
            type,
            title,
            status,
            detail,
            context.TraceIdentifier,
            null));
    }

    private static (int Status, string Title, string Type) Map(Exception exception) => exception switch
    {
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://shenok.dev/problems/unauthorized"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", "https://shenok.dev/problems/forbidden"),
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found", "https://shenok.dev/problems/not-found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict", "https://shenok.dev/problems/conflict"),
        DomainException => (StatusCodes.Status400BadRequest, "Domain rule violated", "https://shenok.dev/problems/domain"),
        UnavailableException => (StatusCodes.Status503ServiceUnavailable, "Service Unavailable", "https://shenok.dev/problems/unavailable"),
        _ => (StatusCodes.Status500InternalServerError, "Server error", "https://shenok.dev/problems/server")
    };
}
