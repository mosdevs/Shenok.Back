using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Playback.UseCases.OpenVoiceSegment;

public sealed record OpenVoiceSegmentRequest(Guid VoiceId, string? RelativePath);

/// <summary>
/// An open media segment. The caller owns <see cref="Content"/> and must dispose it.
/// </summary>
public sealed record OpenVoiceSegmentResponse(Stream Content, long Length);

public sealed class OpenVoiceSegmentValidator : AbstractValidator<OpenVoiceSegmentRequest>
{
    public OpenVoiceSegmentValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
    }
}

public sealed class OpenVoiceSegmentUseCase(
    IContentCaller caller,
    IVoiceRepository voices,
    IBlobStorage blobs) : IUseCase<OpenVoiceSegmentRequest, OpenVoiceSegmentResponse>
{
    public async Task<OpenVoiceSegmentResponse> ExecuteAsync(
        OpenVoiceSegmentRequest request,
        CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        voice.EnsureReadyForPlayback();

        var segmentPrefix = voice.Audio.SegmentPrefix
            ?? throw new ConflictException("Segment path is invalid.");
        if (!SegmentPath.TryStorageKey(request.RelativePath, segmentPrefix, out var storageKey))
            throw new ConflictException("Segment path is invalid.");

        var opened = await blobs.OpenReadAsync(storageKey, cancellationToken);
        if (opened is null)
            throw new NotFoundException("Segment was not found.");

        return new OpenVoiceSegmentResponse(opened.Content, opened.Length);
    }
}
