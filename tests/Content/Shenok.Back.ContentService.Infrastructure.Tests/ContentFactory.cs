using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Infrastructure.Tests;

internal static class ContentFactory
{
    private static long _nextCode = 1_000;

    public static Page DraftPage(OwnerId? owner = null) =>
        Page.CreateDraft(
            owner ?? OwnerId.New(),
            PageTitle.Create("Night Walks"),
            PageDescription.Create("A quiet show."),
            CoverImageUrl.Create("https://cdn.example.com/cover.jpg"));

    public static Voice PendingVoice(PageId pageId, string? sourceKey = null) =>
        Voice.CreatePending(
            pageId,
            VoiceTitle.Create("Episode"),
            sourceKey ?? $"sources/{Guid.NewGuid():N}",
            "audio/wav",
            2048);

    public static Voice ReadyVoice(PageId pageId)
    {
        var packageId = Guid.NewGuid().ToString("N");
        var voice = PendingVoice(pageId);
        voice.CompletePackagingSuccess(12.5, $"packages/{packageId}/index.m3u8", $"packages/{packageId}/");
        return voice;
    }

    public static Voice FailedVoice(PageId pageId)
    {
        var voice = PendingVoice(pageId);
        voice.CompletePackagingFailure();
        return voice;
    }

    public static WaveformMapping PageMapping(PageId pageId, long? code = null) =>
        WaveformMapping.RegisterForPage(WaveformCode.From(code ?? NextCode()), pageId, LayoutVersion.V1, pageIsArchived: false);

    public static long NextCode() => Interlocked.Increment(ref _nextCode);
}
