using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;

namespace Shenok.Back.EngagementService.Api.Tests;

public sealed class UseCaseHttpTests
{
    [Fact]
    public async Task Cancelled_validation_does_not_run_the_use_case()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<CanPlayUseCase>(includeInternalTypes: true);
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            UseCaseHttp.OkAsync(
                http,
                new CanPlayRequest(Guid.NewGuid(), Guid.NewGuid()),
                (_, _) =>
                {
                    ran = true;
                    return Task.FromResult(new CanPlayResponse(true));
                },
                cancellation.Token));

        Assert.False(ran);
    }
}
