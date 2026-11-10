using FluentValidation;
using Microsoft.Extensions.Logging;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Exceptions;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;

public sealed record DecodeWaveformRequest(string ContentType, byte[] Image);

public sealed record DecodeWaveformResponse(Guid WaveformId, string TargetType, Guid TargetId);

public sealed class DecodeWaveformValidator : AbstractValidator<DecodeWaveformRequest>
{
    public const int MaxImageBytes = 8 * 1024 * 1024;

    public DecodeWaveformValidator()
    {
        RuleFor(request => request.ContentType)
            .Must(IsAllowedContentType)
            .WithMessage("Content type must be image/jpeg or image/png.");

        RuleFor(request => request.Image)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(image => image!.Length is >= 1 and <= MaxImageBytes)
            .WithMessage($"Image must be between 1 and {MaxImageBytes} bytes.");
    }

    /// <summary>
    /// JPEG or PNG with no parameters. <c>image/jpeg; charset=binary</c> is rejected.
    /// Comparison is ordinal and case-insensitive.
    /// </summary>
    public static bool IsAllowedContentType(string? contentType)
        => contentType is not null
           && (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
               || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Resolves a camera image to the active index row.
/// A decode failure, a code outside the 40-bit window, an unknown code, and a revoked code
/// are the same <see cref="NotFoundException"/> with detail <see cref="MissDetail"/>.
/// The lookup is the no-tracking <c>FindAsync</c> read. A scan does not open an inbox row and does not write the index.
/// </summary>
public sealed class DecodeWaveformUseCase(
    IWaveformDecoder decoder,
    IWaveformIndexRepository index,
    ILogger<DecodeWaveformUseCase> logger) : IUseCase<DecodeWaveformRequest, DecodeWaveformResponse>
{
    public const string MissDetail = "No waveform matched.";

    private const string DecodeFailed = "decode_failed";
    private const string UnknownCode = "unknown_code";
    private const string Revoked = "revoked";

    public async Task<DecodeWaveformResponse> ExecuteAsync(
        DecodeWaveformRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decoded = await decoder.DecodeAsync(request.Image, cancellationToken);
        switch (decoded)
        {
            case WaveformDecodeResult.Failure failure:
                LogDecodeFailed(StableDetail(failure.Reason));
                throw new NotFoundException(MissDetail);
            case WaveformDecodeResult.Success success:
                return await MatchAsync(success.Code, cancellationToken);
            default:
                throw new InvalidOperationException("The waveform decoder returned an unknown result.");
        }
    }

    private async Task<DecodeWaveformResponse> MatchAsync(long codeValue, CancellationToken cancellationToken)
    {
        WaveformCode code;
        try
        {
            code = WaveformCode.From(codeValue);
        }
        catch (InvalidWaveformCodeException)
        {
            LogDecodeFailed(detail: null);
            throw new NotFoundException(MissDetail);
        }

        var stored = await index.FindAsync(code, cancellationToken);
        if (stored is { IsActive: true })
        {
            return new DecodeWaveformResponse(
                stored.WaveformId.Value,
                stored.TargetType.ToString(),
                stored.TargetId);
        }

        LogMiss(stored is { Status: WaveformIndexStatus.Revoked } ? Revoked : UnknownCode);
        throw new NotFoundException(MissDetail);
    }

    /// <summary>
    /// The four decoder words are safe to log. Anything else, including an exception message, is dropped.
    /// </summary>
    private static string? StableDetail(string? reason)
        => reason is WaveformDecodeResult.AnchorNotFound
            or WaveformDecodeResult.CodecFailed
            or WaveformDecodeResult.ImageUnreadable
            or WaveformDecodeResult.Dimensions
            ? reason
            : null;

    private void LogDecodeFailed(string? detail)
    {
        if (detail is null)
        {
            logger.LogInformation("No waveform matched. Reason: {Reason}.", DecodeFailed);
            return;
        }

        logger.LogInformation(
            "No waveform matched. Reason: {Reason}. Detail: {Detail}.",
            DecodeFailed,
            detail);
    }

    private void LogMiss(string reason)
        => logger.LogInformation("No waveform matched. Reason: {Reason}.", reason);
}
