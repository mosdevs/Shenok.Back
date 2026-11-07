using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.ContentService.Api.Http;
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
}
