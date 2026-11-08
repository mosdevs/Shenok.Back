using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;
using Shenok.Back.EngagementService.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;
using Shenok.Back.EngagementService.Application.Listening.UseCases.ReportProgress;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;
using Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;
using Shenok.Back.EngagementService.Domain.Exceptions;

namespace Shenok.Back.EngagementService.Application.Tests;

public class ValidatorAndRegistrationTests
{
    [Fact]
    public async Task Validators_reject_empty_guids_and_the_use_case_still_rejects_them()
    {
        var user = Guid.NewGuid();
        var page = Guid.NewGuid();
        var voice = Guid.NewGuid();
        var waveform = Guid.NewGuid();

        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest("scan", Guid.Empty, waveform, voice, page)).IsValid);
        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest("scan", user, Guid.Empty, voice, page)).IsValid);
        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest("scan", user, waveform, Guid.Empty, page)).IsValid);
        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest("scan", user, waveform, voice, Guid.Empty)).IsValid);
        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest(" ", user, waveform, voice, page)).IsValid);
        Assert.False(new RegisterScanValidator().Validate(new RegisterScanRequest(new string('a', 65), user, waveform, voice, page)).IsValid);
        Assert.True(new RegisterScanValidator().Validate(new RegisterScanRequest(" scan ", user, waveform, voice, page)).IsValid);

        Assert.False(new FollowPageValidator().Validate(new FollowPageRequest(Guid.Empty, page)).IsValid);
        Assert.False(new UnfollowPageValidator().Validate(new UnfollowPageRequest(user, Guid.Empty)).IsValid);
        Assert.False(new CanPlayValidator().Validate(new CanPlayRequest(Guid.Empty, page)).IsValid);
        Assert.False(new ReportProgressValidator().Validate(new ReportProgressRequest(Guid.Empty, voice, "play", 0, false, false)).IsValid);
        Assert.False(new ReportProgressValidator().Validate(new ReportProgressRequest(user, Guid.Empty, "play", 0, false, false)).IsValid);
        Assert.False(new ReportProgressValidator().Validate(new ReportProgressRequest(user, voice, " ", 0, false, false)).IsValid);
        Assert.False(new ReportProgressValidator().Validate(new ReportProgressRequest(user, voice, "play", -1, false, false)).IsValid);
        Assert.True(new ReportProgressValidator().Validate(new ReportProgressRequest(user, voice, " play ", 0, false, true)).IsValid);

        Assert.False(new GetListeningStateValidator().Validate(new GetListeningStateRequest(Guid.Empty, page, [])).IsValid);
        Assert.False(new GetListeningStateValidator().Validate(new GetListeningStateRequest(user, page, [Guid.Empty])).IsValid);
        Assert.True(new GetListeningStateValidator().Validate(new GetListeningStateRequest(user, page, [])).IsValid);
        var tooMany = Enumerable.Range(0, EngagementLimits.MaxVoicesPerRequest + 1).Select(_ => Guid.NewGuid()).ToArray();
        var atTheCap = tooMany.Take(EngagementLimits.MaxVoicesPerRequest).ToArray();
        Assert.False(new GetListeningStateValidator().Validate(new GetListeningStateRequest(user, page, tooMany)).IsValid);
        Assert.True(new GetListeningStateValidator().Validate(new GetListeningStateRequest(user, page, atTheCap)).IsValid);

        var harnessForVoices = new EngagementHarness();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            harnessForVoices.ListeningState().ExecuteAsync(
                new GetListeningStateRequest(harnessForVoices.UserId, harnessForVoices.PageId, tooMany),
                CancellationToken.None));

        var harness = new EngagementHarness();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CanPlay().ExecuteAsync(new CanPlayRequest(Guid.Empty, page), CancellationToken.None));
        await Assert.ThrowsAsync<NegativePlaybackPositionException>(() =>
            harness.ReportProgress().ExecuteAsync(harness.Progress("play", -1), CancellationToken.None));
    }

    [Fact]
    public void AddEngagementApplication_binds_options_and_registers_each_use_case_once()
    {
        var enabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Engagement:AutoFollowOnScan"] = "true" })
            .Build();
        var services = new ServiceCollection();
        services.AddEngagementApplication(enabled);
        services.AddEngagementApplication(enabled);

        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IUseCase<CanPlayRequest, CanPlayResponse>)));
        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(CanPlayUseCase)));
        Assert.Equal(1, services.Count(descriptor => descriptor.ServiceType == typeof(IUseCase<FollowPageRequest>)));

        using var provider = services.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<IOptions<EngagementOptions>>().Value.AutoFollowOnScan);

        var defaults = new ServiceCollection();
        defaults.AddEngagementApplication(new ConfigurationBuilder().Build());
        using var defaultProvider = defaults.BuildServiceProvider();
        Assert.False(defaultProvider.GetRequiredService<IOptions<EngagementOptions>>().Value.AutoFollowOnScan);
    }
}
