using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Domain.Tests;

internal static class ContentFixtures
{
    public static Page DraftPage(
        OwnerId? owner = null,
        string title = "Night Walks",
        string? description = "A quiet show.",
        string? cover = "https://cdn.example.com/cover.jpg")
        => Page.CreateDraft(
            owner ?? OwnerId.New(),
            PageTitle.Create(title),
            PageDescription.Create(description),
            CoverImageUrl.Create(cover));

    public static Voice PendingVoice(PageId pageId, string title = "Episode 1")
        => Voice.CreatePending(
            pageId,
            VoiceTitle.Create(title),
            "sources/episode.wav",
            "audio/wav",
            2048);

    public static Voice ReadyVoice(PageId pageId, string title = "Episode 1", double durationSeconds = 12.5)
    {
        var voice = PendingVoice(pageId, title);
        voice.CompletePackagingSuccess(durationSeconds, "hls/playlist.m3u8", "hls/seg/");
        return voice;
    }

    public static Voice FailedVoice(PageId pageId, string title = "Episode 1")
    {
        var voice = PendingVoice(pageId, title);
        voice.CompletePackagingFailure();
        return voice;
    }

    public static WaveformMapping ActivePageMapping(PageId pageId, long code = 42, bool pageIsArchived = false)
        => WaveformMapping.RegisterForPage(
            WaveformCode.From(code),
            pageId,
            LayoutVersion.V1,
            pageIsArchived);

    public static Dictionary<VoiceId, PackagingStatus> Readiness(params (VoiceId Id, PackagingStatus Status)[] entries)
        => entries.ToDictionary(entry => entry.Id, entry => entry.Status);

    public static void AddVoice(this Page page, VoiceId voiceId, PackagingStatus status = PackagingStatus.Pending)
        => page.AddVoiceReference(voiceId, status);

    public static void AddVoice(this Page page, Voice voice)
        => page.AddVoiceReference(voice.Id, voice.Status);
}
