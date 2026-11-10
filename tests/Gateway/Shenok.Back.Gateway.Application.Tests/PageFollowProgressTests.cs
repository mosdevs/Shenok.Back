using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;
using Shenok.Back.Gateway.Application.Follows.UseCases.FollowPage;
using Shenok.Back.Gateway.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.Gateway.Application.Listening.UseCases.ReportProgress;
using Shenok.Back.Gateway.Application.Pages.UseCases.OpenPage;
using Shenok.Back.Gateway.Application.Playback.UseCases.OpenPlaylist;
using Shenok.Back.Gateway.Application.Playback.UseCases.OpenSegment;
using Shenok.Back.Gateway.Application.Playback.UseCases.StartPlayback;

namespace Shenok.Back.Gateway.Application.Tests;

public sealed class OpenPageTests
{
    [Fact]
    public async Task Locked_listener_is_rejected_without_the_page_body()
    {
        var (content, engagement, pageId, userId, _) = Scene();
        engagement.Unlocked = false;
        var useCase = new OpenPageUseCase(content, engagement);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new OpenPageRequest(userId, pageId, IsAdmin: false), CancellationToken.None));

        Assert.Equal(GatewayLimits.LockedDetail, exception.Message);
    }

    [Fact]
    public async Task Owner_sees_counters_and_listening_state_without_playback_urls()
    {
        var (content, engagement, pageId, _, ownerId) = Scene();
        engagement.Unlocked = false;
        engagement.Following = true;
        var useCase = new OpenPageUseCase(content, engagement);

        var response = await useCase.ExecuteAsync(new OpenPageRequest(ownerId, pageId, IsAdmin: false), CancellationToken.None);

        Assert.Equal(42, response.Page.SubscriberCount);
        Assert.Equal("https://cdn.example/cover.jpg", response.Page.CoverUrl);
        Assert.True(response.IsFollowing);
        Assert.Equal(2, response.Voices.Count);
        Assert.Equal("One", response.Voices[0].Title);
        Assert.True(response.Voices[0].IsCompleted);
        Assert.Equal(180, response.Voices[0].PositionSeconds);
        Assert.Equal(180.5, response.Voices[0].Duration);
        Assert.False(response.Voices[1].IsCompleted);
        Assert.Equal(0, response.Voices[1].PositionSeconds);
    }

    [Fact]
    public async Task Admin_can_open_a_locked_page()
    {
        var (content, engagement, pageId, userId, _) = Scene();
        engagement.Unlocked = false;
        var useCase = new OpenPageUseCase(content, engagement);

        var response = await useCase.ExecuteAsync(new OpenPageRequest(userId, pageId, IsAdmin: true), CancellationToken.None);

        Assert.Equal(pageId, response.Page.Id);
    }

    private static (FakeContent Content, FakeEngagement Engagement, Guid PageId, Guid UserId, Guid OwnerId) Scene()
    {
        var pageId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var content = new FakeContent
        {
            Page = new CatalogPage(
                pageId,
                ownerId,
                "Show",
                "https://cdn.example/cover.jpg",
                42,
                [
                    new CatalogVoice(first, "One", 180.5, GatewayLimits.ReadyPackaging),
                    new CatalogVoice(second, "Two", 20, "Pending")
                ])
        };
        var engagement = new FakeEngagement();
        engagement.Listening.Add(new ListeningVoice(first, true, 180));
        return (content, engagement, pageId, Guid.NewGuid(), ownerId);
    }
}

public sealed class FollowAndProgressTests
{
    [Fact]
    public async Task Follow_and_unfollow_delegate_to_engagement()
    {
        var engagement = new FakeEngagement();
        var pageId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await new FollowPageUseCase(engagement).ExecuteAsync(new FollowPageRequest(userId, pageId), CancellationToken.None);
        await new UnfollowPageUseCase(engagement).ExecuteAsync(new UnfollowPageRequest(userId, pageId), CancellationToken.None);

        Assert.Equal([pageId], engagement.Follows);
        Assert.Equal([pageId], engagement.Unfollows);
    }

    [Fact]
    public async Task Locked_listener_does_not_report_progress()
    {
        var (content, engagement, voiceId, userId, _) = Scene();
        engagement.Unlocked = false;
        var useCase = new ReportProgressUseCase(content, engagement);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(Progress(userId, voiceId, isAdmin: false), CancellationToken.None));

        Assert.Equal(GatewayLimits.LockedDetail, exception.Message);
        Assert.Empty(engagement.Progress);
        Assert.Single(engagement.CanPlayPages);
    }

    [Fact]
    public async Task Unlocked_listener_reports_the_trimmed_session_once()
    {
        var (content, engagement, voiceId, userId, _) = Scene();
        engagement.Unlocked = true;
        var useCase = new ReportProgressUseCase(content, engagement);

        await useCase.ExecuteAsync(Progress(userId, voiceId, isAdmin: false), CancellationToken.None);

        var report = Assert.Single(engagement.Progress);
        Assert.Equal(voiceId, report.VoiceId);
        Assert.Equal("session-1", report.SessionId);
        Assert.Equal(9, report.Position);
        Assert.True(report.IsSeek);
    }

    [Fact]
    public async Task Owner_reports_progress_without_an_unlock_check()
    {
        var (content, engagement, voiceId, _, ownerId) = Scene();
        engagement.Unlocked = false;
        var useCase = new ReportProgressUseCase(content, engagement);

        await useCase.ExecuteAsync(Progress(ownerId, voiceId, isAdmin: false), CancellationToken.None);

        Assert.Empty(engagement.CanPlayPages);
        Assert.Single(engagement.Progress);
    }

    [Fact]
    public async Task Follow_and_progress_do_not_put_the_user_id_on_the_engagement_call()
    {
        var (content, engagement, voiceId, userId, _) = Scene();
        engagement.Unlocked = true;
        var page = content.Page ?? throw new InvalidOperationException("The test page was not arranged.");
        var pageId = page.PageId;

        await new FollowPageUseCase(engagement).ExecuteAsync(new FollowPageRequest(userId, pageId), CancellationToken.None);
        await new ReportProgressUseCase(content, engagement).ExecuteAsync(
            Progress(userId, voiceId, isAdmin: false),
            CancellationToken.None);

        Assert.Equal([pageId], engagement.Follows);
        Assert.DoesNotContain(userId, engagement.Follows);
        var report = Assert.Single(engagement.Progress);
        Assert.NotEqual(userId, report.VoiceId);
        Assert.DoesNotContain(userId.ToString("D"), report.SessionId, StringComparison.Ordinal);
    }

    private static ReportProgressRequest Progress(Guid userId, Guid voiceId, bool isAdmin) =>
        new(userId, voiceId, "  session-1  ", 9, false, true, isAdmin);

    private static (FakeContent Content, FakeEngagement Engagement, Guid VoiceId, Guid UserId, Guid OwnerId) Scene()
    {
        var pageId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var content = new FakeContent
        {
            Voice = new CatalogVoiceDetail(voiceId, pageId, "One", 10, GatewayLimits.ReadyPackaging),
            Page = new CatalogPage(pageId, ownerId, "Show", null, 1, [])
        };
        return (content, new FakeEngagement(), voiceId, Guid.NewGuid(), ownerId);
    }
}

public sealed class OpenPlaylistTests
{
    [Fact]
    public async Task Expired_or_unsigned_link_does_not_call_content()
    {
        var content = new FakeContent();
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenPlaylistUseCase(new FakeLinks(), content, clock);
        var voiceId = Guid.NewGuid();
        var waveformId = Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new OpenPlaylistRequest(voiceId, waveformId, 1_700_000_000, "test"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new OpenPlaylistRequest(voiceId, waveformId, 1_700_000_200, "nope"), CancellationToken.None));

        Assert.Empty(content.Playlists);
    }

    [Fact]
    public async Task Signed_link_returns_the_playlist_body()
    {
        var content = new FakeContent { Playlist = new("#EXTM3U\nseg.ts\n", 1_700_000_200) };
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenPlaylistUseCase(new FakeLinks(), content, clock);
        var voiceId = Guid.NewGuid();
        var waveformId = Guid.NewGuid();

        var opened = await useCase.ExecuteAsync(
            new OpenPlaylistRequest(voiceId, waveformId, 1_700_000_200, "test"),
            CancellationToken.None);

        Assert.Equal("#EXTM3U\nseg.ts\n", opened.Playlist);
        Assert.Equal((voiceId, waveformId), Assert.Single(content.Playlists));
    }

    [Fact]
    public async Task Playlist_without_a_waveform_issues_a_direct_playlist()
    {
        var content = new FakeContent { Playlist = new("#EXTM3U\nseg.ts\n", 1_700_000_200) };
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenPlaylistUseCase(new FakeLinks(), content, clock);
        var voiceId = Guid.NewGuid();

        var opened = await useCase.ExecuteAsync(
            new OpenPlaylistRequest(voiceId, null, 1_700_000_200, "test"),
            CancellationToken.None);

        Assert.Equal("#EXTM3U\nseg.ts\n", opened.Playlist);
        Assert.Equal((voiceId, (Guid?)null), Assert.Single(content.Playlists));
    }

    [Fact]
    public async Task Empty_waveform_id_does_not_call_content()
    {
        var content = new FakeContent();
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenPlaylistUseCase(new FakeLinks(), content, clock);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(
                new OpenPlaylistRequest(Guid.NewGuid(), Guid.Empty, 1_700_000_200, "test"),
                CancellationToken.None));

        Assert.Empty(content.Playlists);
    }
}

public sealed class StartPlaybackTests
{
    [Fact]
    public async Task Owner_starts_playback_without_a_waveform_and_without_an_unlock_check()
    {
        var (content, engagement, voiceId, _, ownerId) = Scene();
        var useCase = new StartPlaybackUseCase(content, engagement, new FakeLinks());

        var response = await useCase.ExecuteAsync(new StartPlaybackRequest(ownerId, voiceId), CancellationToken.None);

        Assert.Contains("sig=test", response.AudioUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("waveformId", response.AudioUrl, StringComparison.Ordinal);
        Assert.Equal(10, response.Duration);
        Assert.Equal(1_700_000_000, response.ExpiresUnixSeconds);
        Assert.Equal((voiceId, (Guid?)null), Assert.Single(content.Playlists));
        Assert.Empty(engagement.CanPlayPages);
    }

    [Fact]
    public async Task Unlocked_listener_starts_playback()
    {
        var (content, engagement, voiceId, userId, _) = Scene();
        engagement.Unlocked = true;
        var useCase = new StartPlaybackUseCase(content, engagement, new FakeLinks());

        var response = await useCase.ExecuteAsync(new StartPlaybackRequest(userId, voiceId), CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.AudioUrl));
        Assert.Equal((voiceId, (Guid?)null), Assert.Single(content.Playlists));
        Assert.Single(engagement.CanPlayPages);
    }

    [Fact]
    public async Task Stranger_is_rejected_before_a_playlist_is_issued()
    {
        var (content, engagement, voiceId, userId, _) = Scene();
        engagement.Unlocked = false;
        var useCase = new StartPlaybackUseCase(content, engagement, new FakeLinks());

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new StartPlaybackRequest(userId, voiceId), CancellationToken.None));

        Assert.Equal(GatewayLimits.LockedDetail, exception.Message);
        Assert.Empty(content.Playlists);
        Assert.Single(engagement.CanPlayPages);
    }

    private static (FakeContent Content, FakeEngagement Engagement, Guid VoiceId, Guid UserId, Guid OwnerId) Scene()
    {
        var pageId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var content = new FakeContent
        {
            Voice = new CatalogVoiceDetail(voiceId, pageId, "One", 10, GatewayLimits.ReadyPackaging),
            Page = new CatalogPage(pageId, ownerId, "Show", null, 1, [])
        };
        return (content, new FakeEngagement(), voiceId, Guid.NewGuid(), ownerId);
    }
}

public sealed class OpenSegmentTests
{
    [Fact]
    public async Task Valid_signature_opens_the_segment_once()
    {
        var content = new FakeContent();
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenSegmentUseCase(new FakeLinks(), content, clock);
        var voiceId = Guid.NewGuid();

        var opened = await useCase.ExecuteAsync(
            new OpenSegmentRequest(voiceId, "seg_00000.ts", 1_700_000_200, "test"),
            CancellationToken.None);

        await using (opened.Content)
        {
            Assert.Equal(3, opened.Length);
            Assert.Equal("seg_00000.ts", Assert.Single(content.Segments));
        }
    }

    [Theory]
    [InlineData(1_700_000_000, "test")]
    [InlineData(1_700_000_200, "nope")]
    public async Task Expired_or_bad_signature_does_not_call_content(long expires, string signature)
    {
        var content = new FakeContent();
        var clock = new FixedClock(DateTimeOffset.FromUnixTimeSeconds(1_700_000_100));
        var useCase = new OpenSegmentUseCase(new FakeLinks(), content, clock);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(
                new OpenSegmentRequest(Guid.NewGuid(), "seg_00000.ts", expires, signature),
                CancellationToken.None));

        Assert.Empty(content.Segments);
    }
}
