using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using WaveformBits = Shenok.Back.WaveformCodec.WaveformCodec;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Shenok.Back.RecognitionService.Infrastructure.Imaging;

/// <summary>
/// Reads a camera image down to a waveform code. It samples the 24 bar centerlines.
/// It does not allocate a second warped card.
/// </summary>
public sealed class ImageSharpWaveformDecoder : IWaveformDecoder
{
    private const int MinSide = 64;
    private const int MaxSide = 4096;
    private const int WorkingLongSide = 1200;
    private const int SamplesPerBar = 128;

    public ValueTask<WaveformDecodeResult> DecodeAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<WaveformDecodeResult>(Decode(image.Span, cancellationToken));
    }

    private static WaveformDecodeResult Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        Image<Rgba32> image;
        try
        {
            var format = Image.DetectFormat(bytes);
            if (format is not JpegFormat and not PngFormat)
                return new WaveformDecodeResult.Failure(WaveformDecodeResult.ImageUnreadable);

            image = Image.Load<Rgba32>(bytes);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException or NotSupportedException or ArgumentException)
        {
            return new WaveformDecodeResult.Failure(WaveformDecodeResult.ImageUnreadable);
        }

        using (image)
        {
            if (image.Width is < MinSide or > MaxSide || image.Height is < MinSide or > MaxSide)
                return new WaveformDecodeResult.Failure(WaveformDecodeResult.Dimensions);

            var luminance = Luminance(image);
            var working = WorkingCopy(luminance, image.Width, image.Height, out var workingWidth, out var workingHeight);
            var binary = Threshold(working);

            cancellationToken.ThrowIfCancellationRequested();
            if (!AnchorLocator.TryFind(
                    binary,
                    workingWidth,
                    workingHeight,
                    luminance,
                    image.Width,
                    image.Height,
                    cancellationToken,
                    out var topLeft,
                    out var topRight,
                    out var bottomRight,
                    out var bottomLeft))
            {
                return new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound);
            }

            if (!Homography.TrySolve(topLeft, topRight, bottomRight, bottomLeft, out var homography))
                return new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound);

            Span<byte> bars = stackalloc byte[WaveformBits.BarCount];
            for (var index = 0; index < bars.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadBar(luminance, image.Width, image.Height, homography, index, out var level))
                    return new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound);

                bars[index] = level;
            }

            return WaveformBits.TryDecode(bars, out var code)
                ? new WaveformDecodeResult.Success((long)code)
                : new WaveformDecodeResult.Failure(WaveformDecodeResult.CodecFailed);
        }
    }

    private static bool TryReadBar(
        ReadOnlySpan<byte> luminance,
        int width,
        int height,
        Homography homography,
        int index,
        out byte level)
    {
        level = 0;
        var outside = 0;
        var ink = 0;
        var x = LayoutV1Geometry.BarCenterX(index);
        for (var sample = 0; sample < SamplesPerBar; sample++)
        {
            var y = LayoutV1Geometry.BaselineY - ((sample + 0.5) * 0.25);
            var (pixelX, pixelY) = homography.Map(x, y);
            if (double.IsNaN(pixelX) || double.IsNaN(pixelY) || pixelX < 0 || pixelY < 0 || pixelX >= width || pixelY >= height)
            {
                outside++;
                continue;
            }

            if (Sample(luminance, width, height, pixelX, pixelY) < LayoutV1Geometry.InkThreshold)
                ink++;
        }

        if (outside > SamplesPerBar / 4)
            return false;

        var modules = ink * 0.25;
        var nearest = 4;
        var bestDistance = double.MaxValue;
        for (var candidate = 4; candidate <= LayoutV1Geometry.BarLaneHeight; candidate += 4)
        {
            var distance = Math.Abs(modules - candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        level = (byte)((nearest / 4) - 1);
        return true;
    }

    private static byte Sample(ReadOnlySpan<byte> luminance, int width, int height, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(x0 + 1, width - 1);
        var y1 = Math.Min(y0 + 1, height - 1);
        x0 = Math.Clamp(x0, 0, width - 1);
        y0 = Math.Clamp(y0, 0, height - 1);
        var tx = x - Math.Floor(x);
        var ty = y - Math.Floor(y);
        var top = (luminance[(y0 * width) + x0] * (1 - tx)) + (luminance[(y0 * width) + x1] * tx);
        var bottom = (luminance[(y1 * width) + x0] * (1 - tx)) + (luminance[(y1 * width) + x1] * tx);
        return (byte)Math.Clamp((int)Math.Round((top * (1 - ty)) + (bottom * ty)), 0, 255);
    }

    private static byte[] Luminance(Image<Rgba32> image)
    {
        var buffer = new byte[image.Width * image.Height];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var offset = y * accessor.Width;
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    buffer[offset + x] = (byte)Math.Clamp(
                        (int)Math.Round((0.299 * pixel.R) + (0.587 * pixel.G) + (0.114 * pixel.B)),
                        0,
                        255);
                }
            }
        });
        return buffer;
    }

    private static byte[] WorkingCopy(byte[] source, int width, int height, out int workingWidth, out int workingHeight)
    {
        var longSide = Math.Max(width, height);
        if (longSide <= WorkingLongSide)
        {
            workingWidth = width;
            workingHeight = height;
            return source;
        }

        var factor = WorkingLongSide / (double)longSide;
        workingWidth = Math.Max(1, (int)Math.Round(width * factor));
        workingHeight = Math.Max(1, (int)Math.Round(height * factor));
        var destination = new byte[workingWidth * workingHeight];
        for (var y = 0; y < workingHeight; y++)
        {
            var y0 = y * height / workingHeight;
            var y1 = Math.Max(y0 + 1, (y + 1) * height / workingHeight);
            for (var x = 0; x < workingWidth; x++)
            {
                var x0 = x * width / workingWidth;
                var x1 = Math.Max(x0 + 1, (x + 1) * width / workingWidth);
                long sum = 0;
                var count = 0;
                for (var yy = y0; yy < y1; yy++)
                {
                    var row = yy * width;
                    for (var xx = x0; xx < x1; xx++)
                    {
                        sum += source[row + xx];
                        count++;
                    }
                }

                destination[(y * workingWidth) + x] = (byte)(sum / count);
            }
        }

        return destination;
    }

    private static byte[] Threshold(ReadOnlySpan<byte> luminance)
    {
        var binary = new byte[luminance.Length];
        for (var index = 0; index < luminance.Length; index++)
            binary[index] = luminance[index] < LayoutV1Geometry.InkThreshold ? (byte)1 : (byte)0;

        return binary;
    }
}
