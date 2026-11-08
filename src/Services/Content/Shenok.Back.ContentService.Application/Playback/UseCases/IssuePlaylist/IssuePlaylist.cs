using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Playback;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;

public sealed record IssuePlaylistRequest(Guid VoiceId, Guid? WaveformId);

public sealed record IssuePlaylistResponse(
    Guid VoiceId,
    Guid PageId,
    string Title,
    double DurationSeconds,
    string Playlist,
    long ExpiresUnixSeconds);

public sealed class IssuePlaylistValidator : AbstractValidator<IssuePlaylistRequest>
{
    public IssuePlaylistValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.WaveformId).Must(id => id is null || id != Guid.Empty);
    }
}

public sealed class IssuePlaylistUseCase(
    IContentCaller caller,
    IVoiceRepository voices,
    IPageRepository pages,
    IWaveformMappingRepository mappings,
    IBlobStorage blobs,
    IPlaylistSigner signer) : IUseCase<IssuePlaylistRequest, IssuePlaylistResponse>
{
    public async Task<IssuePlaylistResponse> ExecuteAsync(IssuePlaylistRequest request, CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        if (request.WaveformId == Guid.Empty)
            throw new NotFoundException("Waveform was not found.");

        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        voice.EnsureReadyForPlayback();

        var page = await ContentMaps.RequirePageAsync(pages, voice.PageId.Value, cancellationToken);
        if (!page.VoiceIds.Contains(voice.Id))
            throw new ForbiddenException("Voice is not on the page.");

        if (request.WaveformId is Guid waveformId)
            await EnsureActiveScanAsync(page, voice.Id.Value, waveformId, cancellationToken);

        var playlistKey = voice.Audio.PlaylistStorageKey
            ?? throw new ConflictException("The packaged playlist cannot be issued.");
        var segmentPrefix = voice.Audio.SegmentPrefix
            ?? throw new ConflictException("The packaged playlist cannot be issued.");
        var body = await blobs.ReadTextAsync(playlistKey, HlsPlaylistSigner.MaxPlaylistChars, cancellationToken);
        var signed = signer.Sign(voice.Id.Value, body, segmentPrefix);
        return new IssuePlaylistResponse(
            voice.Id.Value,
            page.Id.Value,
            voice.Title.Value,
            voice.DurationSeconds!.Value,
            signed.Body,
            signed.ExpiresUnixSeconds);
    }

    private async Task EnsureActiveScanAsync(Page page, Guid voiceId, Guid waveformId, CancellationToken cancellationToken)
    {
        var mapping = await ContentMaps.RequireWaveformAsync(mappings, waveformId, cancellationToken);
        try
        {
            mapping.EnsureActiveForPlayback();
        }
        catch (WaveformNotActiveException exception)
        {
            throw new ConflictException(exception.Message);
        }

        var targetsVoice = mapping.TargetType == WaveformTargetType.Voice && mapping.TargetId == voiceId;
        var targetsPage = mapping.TargetType == WaveformTargetType.Page && mapping.TargetId == page.Id.Value;
        if (!targetsVoice && !targetsPage)
            throw new ForbiddenException("Waveform does not target this voice.");

        if (page.Status == PageStatus.Archived)
            throw new ForbiddenException("Archived pages are hidden from new scans.");
    }
}
