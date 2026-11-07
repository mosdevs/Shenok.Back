using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Common;

internal static class ContentMaps
{
    public static async Task<Page> RequirePageAsync(IPageRepository pages, Guid pageId, CancellationToken cancellationToken)
    {
        if (pageId == Guid.Empty)
            throw new NotFoundException("Page was not found.");

        return await pages.GetByIdAsync(PageId.From(pageId), cancellationToken)
            ?? throw new NotFoundException("Page was not found.");
    }

    public static async Task<Voice> RequireVoiceAsync(IVoiceRepository voices, Guid voiceId, CancellationToken cancellationToken)
    {
        if (voiceId == Guid.Empty)
            throw new NotFoundException("Voice was not found.");

        return await voices.GetByIdAsync(VoiceId.From(voiceId), cancellationToken)
            ?? throw new NotFoundException("Voice was not found.");
    }

    public static async Task<WaveformMapping> RequireWaveformAsync(
        IWaveformMappingRepository mappings,
        Guid waveformId,
        CancellationToken cancellationToken)
    {
        if (waveformId == Guid.Empty)
            throw new NotFoundException("Waveform was not found.");

        return await mappings.GetByIdAsync(WaveformId.From(waveformId), cancellationToken)
            ?? throw new NotFoundException("Waveform was not found.");
    }

    public static VoiceSummary ToSummary(Voice voice) => new(
        voice.Id.Value,
        voice.Title.Value,
        voice.DurationSeconds,
        voice.Status.ToString());

    public static VoiceResponse ToVoice(Voice voice) => new(
        voice.Id.Value,
        voice.PageId.Value,
        voice.Title.Value,
        voice.DurationSeconds,
        voice.Status.ToString());

    public static PageResponse ToPage(Page page, IReadOnlyList<Voice> voices)
    {
        var byId = new Dictionary<VoiceId, Voice>(voices.Count);
        foreach (var voice in voices)
            byId[voice.Id] = voice;

        var ordered = new VoiceSummary[page.VoiceIds.Count];
        for (var i = 0; i < page.VoiceIds.Count; i++)
        {
            var voiceId = page.VoiceIds[i];
            if (!byId.TryGetValue(voiceId, out var voice))
                throw new NotFoundException($"Voice {voiceId.Value} was not found.");

            ordered[i] = ToSummary(voice);
        }

        return new PageResponse(
            page.Id.Value,
            page.OwnerId.Value,
            page.Title.Value,
            page.Description.Value,
            page.CoverImageUrl?.Value,
            page.Status.ToString(),
            page.CreatedAt,
            ordered);
    }

    public static async Task<PageResponse> LoadPageAsync(Page page, IVoiceRepository voices, CancellationToken cancellationToken)
    {
        if (page.VoiceIds.Count == 0)
            return ToPage(page, []);

        var loaded = await voices.ListByPageIdsAsync([page.Id], cancellationToken);
        return ToPage(page, loaded);
    }

    public static Dictionary<VoiceId, PackagingStatus> Readiness(Page page, IReadOnlyList<Voice> voices)
    {
        var onPage = page.VoiceIds.ToHashSet();
        var readiness = new Dictionary<VoiceId, PackagingStatus>(onPage.Count);
        foreach (var voice in voices)
        {
            if (onPage.Contains(voice.Id))
                readiness[voice.Id] = voice.Status;
        }

        return readiness;
    }
}
