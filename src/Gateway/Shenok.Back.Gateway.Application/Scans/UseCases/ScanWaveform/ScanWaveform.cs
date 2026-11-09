using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Scans.UseCases.ScanWaveform;

public sealed record ScanWaveformRequest(Guid UserId, string ScanId, string ContentType, byte[] Image);

public sealed record ScanVoiceDocument(Guid Id, string Title, string? AudioUrl, double? Duration);

public sealed record ScanPageDocument(Guid Id, string Title);

public sealed record ScanWaveformResponse(
    ScanVoiceDocument Voice,
    ScanPageDocument Page,
    bool PromptFollow,
    int ResumePositionSeconds);

public sealed class ScanWaveformValidator : AbstractValidator<ScanWaveformRequest>
{
    public ScanWaveformValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ScanId)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= GatewayLimits.ClientTokenLength)
            .WithMessage($"Scan id must be 1 to {GatewayLimits.ClientTokenLength} characters after trim.");
        RuleFor(x => x.ContentType)
            .Must(IsImage)
            .WithMessage("Content type must be image/jpeg or image/png.");
        RuleFor(x => x.Image)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(image => image!.Length is >= 1 and <= GatewayLimits.MaxImageBytes)
            .WithMessage(GatewayLimits.ImageSizeMessage);
    }

    /// <summary>
    /// JPEG or PNG with no parameters. <c>image/jpeg; charset=binary</c> is rejected.
    /// </summary>
    public static bool IsImage(string? contentType) =>
        contentType is not null
        && (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Flow B. A miss stops before Content or Engagement. The scan does not follow.
/// Engagement keeps the scan id unique. This use case does not reject an archived page before unlock;
/// Content still refuses playback for that page when the playlist is issued.
/// </summary>
public sealed class ScanWaveformUseCase(
    IScanDecoder decoder,
    IContentGateway content,
    IEngagementGateway engagement,
    IPlaybackLink links) : IUseCase<ScanWaveformRequest, ScanWaveformResponse>
{
    public async Task<ScanWaveformResponse> ExecuteAsync(
        ScanWaveformRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hit = await decoder.DecodeAsync(new ScanImage(request.ContentType, request.Image), cancellationToken);
        if (hit is null || hit.WaveformId == Guid.Empty || hit.TargetId == Guid.Empty)
            throw new NotFoundException(GatewayLimits.MissDetail);

        var resolved = await ResolveAsync(hit, cancellationToken);
        var unlock = await engagement.RegisterScanAsync(
            request.ScanId.Trim(),
            hit.WaveformId,
            resolved.Voice.VoiceId,
            resolved.Page.PageId,
            cancellationToken);

        string? audioUrl = null;
        if (IsReady(resolved.Voice.PackagingStatus))
        {
            var issued = await content.IssuePlaylistAsync(
                resolved.Voice.VoiceId,
                hit.WaveformId,
                cancellationToken);
            audioUrl = links.PlaylistUrl(resolved.Voice.VoiceId, hit.WaveformId, issued.ExpiresUnixSeconds);
        }

        return new ScanWaveformResponse(
            new ScanVoiceDocument(
                resolved.Voice.VoiceId,
                resolved.Voice.Title,
                audioUrl,
                resolved.Voice.DurationSeconds),
            new ScanPageDocument(resolved.Page.PageId, resolved.Page.Title),
            unlock.PromptFollow,
            unlock.ResumePositionSeconds);
    }

    private async Task<ResolvedTarget> ResolveAsync(WaveformHit hit, CancellationToken cancellationToken)
    {
        if (WaveformTargets.IsPage(hit.TargetType))
        {
            var page = await content.GetPageAsync(hit.TargetId, cancellationToken);
            if (page.Voices.Count == 0)
                throw new NotFoundException(GatewayLimits.EmptyPageDetail);

            return new ResolvedTarget(page, page.Voices[0]);
        }

        if (WaveformTargets.IsVoice(hit.TargetType))
        {
            var voice = await content.GetVoiceAsync(hit.TargetId, cancellationToken);
            var page = await content.GetPageAsync(voice.PageId, cancellationToken);
            return new ResolvedTarget(
                page,
                new CatalogVoice(voice.VoiceId, voice.Title, voice.DurationSeconds, voice.PackagingStatus));
        }

        throw new NotFoundException(GatewayLimits.MissDetail);
    }

    private static bool IsReady(string? status) =>
        string.Equals(status, GatewayLimits.ReadyPackaging, StringComparison.OrdinalIgnoreCase);

    private readonly record struct ResolvedTarget(CatalogPage Page, CatalogVoice Voice);
}
