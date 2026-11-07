using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Application.Tests;

internal static class ApplicationScenes
{
    public static Page Draft(FakeCaller caller, string title = "Show") =>
        Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create(title), PageDescription.Create(null), null);

    public static Voice Pending(Page page, string title = "Episode") =>
        Voice.CreatePending(page.Id, VoiceTitle.Create(title), $"sources/{title}", "audio/wav", 32);

    public static Voice Ready(Page page, string title = "Episode")
    {
        var voice = Pending(page, title);
        voice.CompletePackagingSuccess(9, "hls/playlist.m3u8", "hls/seg/");
        return voice;
    }

    public static async Task<(FakePages Pages, FakeVoices Voices)> Store(Page page, params Voice[] voices)
    {
        var pages = new FakePages();
        var storedVoices = new FakeVoices();
        await pages.AddAsync(page);
        foreach (var voice in voices)
            storedVoices.Add(voice);

        return (pages, storedVoices);
    }
}
