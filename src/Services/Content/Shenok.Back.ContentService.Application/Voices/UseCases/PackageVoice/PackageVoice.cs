using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;

public sealed record PackageVoiceRequest(Guid VoiceId);

public sealed class PackageVoiceValidator : AbstractValidator<PackageVoiceRequest>
{
    public PackageVoiceValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
    }
}

/// <summary>
/// Hosted packager entry point. The request carries only the voice id.
/// Duration, playlist key, and segment prefix come from <see cref="IAudioPackager"/>.
/// </summary>
public sealed class PackageVoiceUseCase(
    IContentCaller caller,
    IVoiceRepository voices,
    IAudioPackager packager,
    IUnitOfWork unitOfWork) : IUseCase<PackageVoiceRequest, VoiceResponse>
{
    public async Task<VoiceResponse> ExecuteAsync(PackageVoiceRequest request, CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        if (voice.Status == PackagingStatus.Ready)
            return ContentMaps.ToVoice(voice);

        var result = await packager.PackageAsync(voice.Audio.SourceStorageKey, AudioLimits.MaxDurationSeconds, cancellationToken);
        var changed = result switch
        {
            AudioPackageResult.Succeeded succeeded when IsIssuable(succeeded.DurationSeconds) =>
                ApplySuccess(voice, succeeded),
            _ => ApplyFailure(voice)
        };

        if (!changed)
            return ContentMaps.ToVoice(voice);

        try
        {
            await voices.UpdateAsync(voice, cancellationToken);
            await unitOfWork.SaveChanges(cancellationToken);
        }
        catch (InvalidPackagingTransitionException)
        {
            var current = await voices.GetByIdAsync(voice.Id, cancellationToken);
            if (current?.Status == PackagingStatus.Ready)
                return ContentMaps.ToVoice(current);

            throw;
        }

        return ContentMaps.ToVoice(voice);
    }

    private static bool ApplySuccess(Voice voice, AudioPackageResult.Succeeded succeeded)
    {
        if (voice.Status == PackagingStatus.Pending)
            voice.CompletePackagingSuccess(succeeded.DurationSeconds, succeeded.PlaylistStorageKey, succeeded.SegmentPrefix);
        else
            voice.RetryPackagingSuccess(succeeded.DurationSeconds, succeeded.PlaylistStorageKey, succeeded.SegmentPrefix);

        return true;
    }

    private static bool ApplyFailure(Voice voice)
    {
        if (voice.Status != PackagingStatus.Pending)
            return false;

        voice.CompletePackagingFailure();
        return true;
    }

    private static bool IsIssuable(double durationSeconds) =>
        double.IsFinite(durationSeconds) && durationSeconds > 0 && durationSeconds <= AudioLimits.MaxDurationSeconds;
}
