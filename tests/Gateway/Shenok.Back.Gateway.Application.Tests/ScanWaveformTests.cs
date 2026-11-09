using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;
using Shenok.Back.Gateway.Application.Scans.UseCases.ScanWaveform;

namespace Shenok.Back.Gateway.Application.Tests;

public sealed class ScanWaveformTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _waveformId = Guid.NewGuid();
    private readonly Guid _pageId = Guid.NewGuid();
    private readonly Guid _firstVoiceId = Guid.NewGuid();
    private readonly Guid _secondVoiceId = Guid.NewGuid();

    [Fact]
    public async Task Miss_does_not_call_content_or_engagement()
    {
        var content = new FakeContent();
        var engagement = new FakeEngagement();
        var useCase = UseCase(new ScriptedDecoder(null), content, engagement);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Request([0xFF, 0xD8]), CancellationToken.None));

        Assert.Equal(GatewayLimits.MissDetail, exception.Message);
        Assert.Empty(content.Pages);
        Assert.Empty(content.Voices);
        Assert.Empty(content.Playlists);
        Assert.Empty(engagement.Scans);
        Assert.Empty(engagement.Follows);
    }

    [Fact]
    public async Task Unknown_target_is_a_miss_and_does_not_unlock()
    {
        var content = new FakeContent();
        var engagement = new FakeEngagement();
        var useCase = UseCase(
            new ScriptedDecoder(new WaveformHit(_waveformId, "Album", _pageId)),
            content,
            engagement);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Request([1]), CancellationToken.None));

        Assert.Equal(GatewayLimits.MissDetail, exception.Message);
        Assert.Empty(engagement.Scans);
        Assert.Empty(content.Pages);
    }

    [Fact]
    public async Task Page_target_uses_the_first_voice_and_does_not_follow()
    {
        var content = PageContent(firstReady: true);
        var engagement = new FakeEngagement { Unlock = new ScanUnlock(true, 12) };
        var useCase = UseCase(PageHit(), content, engagement);

        var response = await useCase.ExecuteAsync(Request("  scan-1  "u8.ToArray()), CancellationToken.None);

        Assert.Equal(_firstVoiceId, response.Voice.Id);
        Assert.Equal("One", response.Voice.Title);
        Assert.Equal(180.5, response.Voice.Duration);
        Assert.Equal(_pageId, response.Page.Id);
        Assert.Equal("Show", response.Page.Title);
        Assert.True(response.PromptFollow);
        Assert.Equal(12, response.ResumePositionSeconds);
        Assert.Contains("sig=test", response.Voice.AudioUrl, StringComparison.Ordinal);
        Assert.Contains("exp=1700000000", response.Voice.AudioUrl, StringComparison.Ordinal);
        var scan = Assert.Single(engagement.Scans);
        Assert.Equal("scan-1", scan.ScanId);
        Assert.Equal(_waveformId, scan.WaveformId);
        Assert.Equal(_firstVoiceId, scan.VoiceId);
        Assert.Equal(_pageId, scan.PageId);
        Assert.Empty(engagement.Follows);
        Assert.Equal((_firstVoiceId, _waveformId), Assert.Single(content.Playlists));
        Assert.Empty(content.Voices);
    }

    [Fact]
    public async Task Pending_voice_unlocks_without_a_playlist()
    {
        var content = PageContent(firstReady: false);
        var engagement = new FakeEngagement();
        var useCase = UseCase(PageHit(), content, engagement);

        var response = await useCase.ExecuteAsync(Request([1]), CancellationToken.None);

        Assert.Null(response.Voice.AudioUrl);
        Assert.Empty(content.Playlists);
        Assert.Single(engagement.Scans);
        Assert.Empty(engagement.Follows);
    }

    [Fact]
    public async Task Empty_page_does_not_unlock()
    {
        var content = new FakeContent
        {
            Page = new CatalogPage(_pageId, Guid.NewGuid(), "Show", null, 0, [])
        };
        var engagement = new FakeEngagement();
        var useCase = UseCase(PageHit(), content, engagement);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Request([1]), CancellationToken.None));

        Assert.Equal(GatewayLimits.EmptyPageDetail, exception.Message);
        Assert.Empty(engagement.Scans);
    }

    [Fact]
    public async Task Voice_target_loads_that_voice_and_its_page()
    {
        var content = new FakeContent
        {
            Voice = new CatalogVoiceDetail(_secondVoiceId, _pageId, "Two", 20, GatewayLimits.ReadyPackaging),
            Page = new CatalogPage(_pageId, Guid.NewGuid(), "Show", null, 3, [])
        };
        var engagement = new FakeEngagement();
        var useCase = UseCase(
            new ScriptedDecoder(new WaveformHit(_waveformId, "voice", _secondVoiceId)),
            content,
            engagement);

        var response = await useCase.ExecuteAsync(Request([1]), CancellationToken.None);

        Assert.Equal(_secondVoiceId, response.Voice.Id);
        Assert.Equal([_secondVoiceId], content.Voices);
        Assert.Equal([_pageId], content.Pages);
        Assert.Equal(_secondVoiceId, Assert.Single(engagement.Scans).VoiceId);
    }

    [Fact]
    public async Task Repeat_calls_register_scan_again_and_still_does_not_follow()
    {
        var content = PageContent(firstReady: false);
        var engagement = new FakeEngagement();
        var useCase = UseCase(PageHit(), content, engagement);

        await useCase.ExecuteAsync(Request([1]), CancellationToken.None);
        await useCase.ExecuteAsync(Request([1]), CancellationToken.None);

        Assert.Equal(2, engagement.Scans.Count);
        Assert.All(engagement.Scans, scan => Assert.Equal("scan-1", scan.ScanId));
        Assert.Empty(engagement.Follows);
    }

    private ScanWaveformUseCase UseCase(ScriptedDecoder decoder, FakeContent content, FakeEngagement engagement) =>
        new(decoder, content, engagement, new FakeLinks());

    private ScriptedDecoder PageHit() =>
        new(new WaveformHit(_waveformId, WaveformTargets.Page, _pageId));

    private FakeContent PageContent(bool firstReady) => new()
    {
        Page = new CatalogPage(
            _pageId,
            Guid.NewGuid(),
            "Show",
            "https://cdn.example/cover.jpg",
            4,
            [
                new CatalogVoice(_firstVoiceId, "One", 180.5, firstReady ? GatewayLimits.ReadyPackaging : "Pending"),
                new CatalogVoice(_secondVoiceId, "Two", 20, GatewayLimits.ReadyPackaging)
            ])
    };

    private ScanWaveformRequest Request(byte[] image) =>
        new(_userId, "  scan-1  ", "image/jpeg", image);
}
