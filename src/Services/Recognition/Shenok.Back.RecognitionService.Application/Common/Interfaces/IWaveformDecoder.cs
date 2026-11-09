namespace Shenok.Back.RecognitionService.Application.Common.Interfaces;

/// <summary>
/// Reads a camera image down to a waveform code. Perspective, threshold, and sampling
/// stay behind this port. The bit decoder is <c>WaveformCodec.TryDecode</c>.
/// </summary>
public interface IWaveformDecoder
{
    ValueTask<WaveformDecodeResult> DecodeAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken);
}

/// <summary>
/// A code the codec accepted, or a stable reason word for the log.
/// The reason is not an HTTP field. Image bytes are never part of either case.
/// </summary>
public abstract record WaveformDecodeResult
{
    /// <summary>No anchor matched the layout version 1 mask.</summary>
    public const string AnchorNotFound = "anchor_not_found";

    /// <summary>The sampled heights were not a correctable layout version 1 code.</summary>
    public const string CodecFailed = "codec_failed";

    /// <summary>The bytes were not a JPEG or PNG ImageSharp could load.</summary>
    public const string ImageUnreadable = "image_unreadable";

    /// <summary>Either side of the image was outside 64..4096 pixels.</summary>
    public const string Dimensions = "dimensions";

    private WaveformDecodeResult()
    {
    }

    public sealed record Success(long Code) : WaveformDecodeResult;

    public sealed record Failure(string Reason) : WaveformDecodeResult;
}
