using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Counters;
using Shenok.Back.ContentService.Application.Counters.UseCases.ApplySubscriberDelta;
using Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;

namespace Shenok.Back.ContentService.Api.Tests;

public sealed class UseCaseHttpTests
{
    [Fact]
    public async Task Composed_update_returns_the_shared_validation_problem()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<CreatePageUseCase>(includeInternalTypes: true);
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            TraceIdentifier = "trace-1"
        };

        var result = await UseCaseHttp.ProblemAsync(
            http,
            new UpdatePageMetadataRequest(Guid.NewGuid(), "  ", null, null),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ApiProblem>(objectResult.Value);
        Assert.Equal("https://shenok.dev/problems/validation", problem.Type);
        Assert.Equal("trace-1", problem.TraceId);
        Assert.NotNull(problem.Errors);
        Assert.Contains("Title", problem.Errors!.Keys);
    }

    [Fact]
    public async Task Valid_request_has_no_problem()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<CreatePageUseCase>(includeInternalTypes: true);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var result = await UseCaseHttp.ProblemAsync(
            http,
            new UpdatePageMetadataRequest(Guid.NewGuid(), "Night Walks", null, null),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Created_response_points_at_the_new_resource()
    {
        var pageId = Guid.NewGuid();
        var result = await UseCaseHttp.CreatedAsync(
            HttpWithValidators(),
            new CreatePageRequest("Night Walks", null, null),
            (_, _) => Task.FromResult(new CreatedPage(pageId)),
            created => $"/pages/{created.PageId:D}",
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Equal($"/pages/{pageId:D}", createdResult.Location);
    }

    [Fact]
    public async Task Invalid_create_does_not_run_the_use_case()
    {
        var ran = false;
        var result = await UseCaseHttp.CreatedAsync(
            HttpWithValidators(),
            new CreatePageRequest("  ", null, null),
            (_, _) =>
            {
                ran = true;
                return Task.FromResult(new CreatedPage(Guid.NewGuid()));
            },
            created => $"/pages/{created.PageId:D}",
            CancellationToken.None);

        Assert.False(ran);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task Ok_returns_the_use_case_payload()
    {
        var result = await UseCaseHttp.OkAsync(
            HttpWithValidators(),
            new UpdatePageMetadataRequest(Guid.NewGuid(), "Night Walks", null, null),
            (_, _) => Task.FromResult("saved"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("saved", ok.Value);
    }

    [Fact]
    public async Task Deferred_counter_is_501_and_other_failures_escape()
    {
        var http = HttpWithValidators();
        http.TraceIdentifier = "trace-501";
        var request = new ApplySubscriberDeltaRequest(Guid.NewGuid(), 1, "follow-1");

        var deferred = await UseCaseHttp.OkOrDeferredAsync(
            http,
            request,
            (_, _) => Task.FromException<string>(new CounterUpdateDeferredException()),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(deferred);
        Assert.Equal(StatusCodes.Status501NotImplemented, objectResult.StatusCode);
        var problem = Assert.IsType<ApiProblem>(objectResult.Value);
        Assert.Equal("https://shenok.dev/problems/not-implemented", problem.Type);
        Assert.Equal("trace-501", problem.TraceId);
        Assert.Equal("Content counters are applied in phase 6 and were not changed.", problem.Detail);

        var escaped = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UseCaseHttp.OkOrDeferredAsync(
                http,
                request,
                (_, _) => Task.FromException<string>(new InvalidOperationException("boom")),
                CancellationToken.None));
        Assert.Equal("boom", escaped.Message);
    }

    private static DefaultHttpContext HttpWithValidators()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<CreatePageUseCase>(includeInternalTypes: true);
        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private sealed record CreatedPage(Guid PageId);
}
