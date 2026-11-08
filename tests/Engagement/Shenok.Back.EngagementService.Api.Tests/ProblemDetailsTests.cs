using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.Application.Abstractions.Exceptions;

namespace Shenok.Back.EngagementService.Api.Tests;

public sealed class ProblemDetailsTests
{
    [Fact]
    public async Task Unknown_failure_hides_the_exception_message()
    {
        var body = await InvokeAsync(new InvalidOperationException("Host=localhost;Password=postgres"));

        Assert.Equal(StatusCodes.Status500InternalServerError, body.Status);
        Assert.Equal("application/problem+json", body.ContentType);
        Assert.Equal("https://shenok.dev/problems/server", body.Document.GetProperty("type").GetString());
        Assert.Equal(ApiProblems.ServerDetail, body.Document.GetProperty("detail").GetString());
        Assert.DoesNotContain("postgres", body.Document.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Persistence_give_up_is_503_and_keeps_the_stable_detail()
    {
        var body = await InvokeAsync(new UnavailableException("The follow could not be saved."));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, body.Status);
        Assert.Equal("application/problem+json", body.ContentType);
        Assert.Equal("https://shenok.dev/problems/unavailable", body.Document.GetProperty("type").GetString());
        Assert.Equal("Service Unavailable", body.Document.GetProperty("title").GetString());
        Assert.Equal("The follow could not be saved.", body.Document.GetProperty("detail").GetString());
    }

    private static async Task<ProblemBody> InvokeAsync(Exception exception)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return new ProblemBody(context.Response.StatusCode, context.Response.ContentType, document.RootElement.Clone());
    }

    private sealed record ProblemBody(int Status, string? ContentType, JsonElement Document);
}
