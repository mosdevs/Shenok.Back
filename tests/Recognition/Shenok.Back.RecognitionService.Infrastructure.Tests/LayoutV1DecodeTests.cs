using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Infrastructure.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WaveformBits = Shenok.Back.WaveformCodec.WaveformCodec;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

public sealed class LayoutV1DecodeTests
{
    private const int Scale = 8;

    private readonly ImageSharpWaveformDecoder _decoder = new();

    [Fact]
    public async Task A_rendered_card_decodes_to_the_same_code()
    {
        var png = LayoutV1Card.Render(WaveformBits.Encode(42), Scale, drawDecorativeAudio: false);

        var decoded = await _decoder.DecodeAsync(png, CancellationToken.None);

        var success = Assert.IsType<WaveformDecodeResult.Success>(decoded);
        Assert.Equal(42, success.Code);
    }

    [Fact]
    public async Task Decorative_strokes_do_not_change_the_code()
    {
        var png = LayoutV1Card.Render(WaveformBits.Encode(42), Scale, drawDecorativeAudio: true);

        var decoded = await _decoder.DecodeAsync(png, CancellationToken.None);

        var success = Assert.IsType<WaveformDecodeResult.Success>(decoded);
        Assert.Equal(42, success.Code);
    }

    [Fact]
    public async Task One_neighboring_bar_is_still_the_same_code()
    {
        var levels = WaveformBits.Encode(42);
        var png = LayoutV1Card.Render(levels, Scale, drawDecorativeAudio: false);
        // Bar 14 is a one-level change the codec still corrects for code 42.
        // Some other single-bar changes are rejected by the codec's first matching codeword.
        const int bar = 14;
        var neighbor = (levels[bar] + 1) & 7;
        png = PaintBar(png, bar, neighbor);

        var decoded = await _decoder.DecodeAsync(png, CancellationToken.None);

        var success = Assert.IsType<WaveformDecodeResult.Success>(decoded);
        Assert.Equal(42, success.Code);
    }

    [Fact]
    public async Task Twelve_white_bars_fail_correction()
    {
        var png = LayoutV1Card.Render(WaveformBits.Encode(42), Scale, drawDecorativeAudio: false);
        for (var index = 0; index < LayoutV1Geometry.BarCount; index += 2)
            png = PaintBar(png, index, level: null);

        var decoded = await _decoder.DecodeAsync(png, CancellationToken.None);

        var failure = Assert.IsType<WaveformDecodeResult.Failure>(decoded);
        Assert.Equal(WaveformDecodeResult.CodecFailed, failure.Reason);
    }

    [Fact]
    public async Task A_mild_trapezoid_decodes_to_the_same_code()
    {
        var png = RenderTrapezoid(WaveformBits.Encode(42));

        var decoded = await _decoder.DecodeAsync(png, CancellationToken.None);

        var success = Assert.IsType<WaveformDecodeResult.Success>(decoded);
        Assert.Equal(42, success.Code);
    }

    [Fact]
    public async Task A_blank_card_has_no_anchor_and_a_tiny_png_is_rejected()
    {
        var blank = Blank(LayoutV1Geometry.CardWidth * Scale, LayoutV1Geometry.CardHeight * Scale);
        var tiny = Blank(32, 32);

        var blankResult = await _decoder.DecodeAsync(blank, CancellationToken.None);
        var tinyResult = await _decoder.DecodeAsync(tiny, CancellationToken.None);

        Assert.Equal(WaveformDecodeResult.AnchorNotFound, Assert.IsType<WaveformDecodeResult.Failure>(blankResult).Reason);
        Assert.Equal(WaveformDecodeResult.Dimensions, Assert.IsType<WaveformDecodeResult.Failure>(tinyResult).Reason);
    }

    /// <summary>
    /// Draws the card through a test-owned trapezoid. The decoder's homography is not used to paint the input.
    /// </summary>
    private static byte[] RenderTrapezoid(ReadOnlySpan<byte> levels)
    {
        const double squeeze = 0.0012;
        const int originX = 48;
        const int originY = 40;
        var maxFactor = 1 + (LayoutV1Geometry.CardHeight * squeeze);
        var width = originX + (int)Math.Ceiling(LayoutV1Geometry.CardWidth * Scale * maxFactor) + 64;
        var height = originY + (LayoutV1Geometry.CardHeight * Scale) + 64;
        var levelBytes = levels.ToArray();
        using var image = new Image<L8>(width, height, new L8(LayoutV1Geometry.Background));
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var moduleY = ((y + 0.5) - originY) / Scale;
                var factor = 1 + (moduleY * squeeze);
                if (factor <= 0)
                    continue;

                for (var x = 0; x < width; x++)
                {
                    var moduleX = ((x + 0.5) - originX) / (Scale * factor);
                    if (LayoutV1Geometry.ContainsInk(moduleX, moduleY, levelBytes))
                        row[x] = new L8(LayoutV1Geometry.Ink);
                }
            }
        });

        return Png(image);
    }

    private static byte[] PaintBar(byte[] png, int index, int? level)
    {
        using var image = Image.Load<L8>(png);
        var left = (LayoutV1Geometry.BarLaneLeft + (index * LayoutV1Geometry.BarPitch)) * Scale;
        var width = LayoutV1Geometry.BarWidth * Scale;
        var bottom = LayoutV1Geometry.BaselineY * Scale;
        var laneTop = (LayoutV1Geometry.BaselineY - LayoutV1Geometry.BarLaneHeight) * Scale;
        var inkTop = level is int value ? bottom - (LayoutV1Geometry.BarHeight(value) * Scale) : bottom;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = laneTop; y < bottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                var ink = level is not null && y >= inkTop;
                var color = new L8(ink ? LayoutV1Geometry.Ink : LayoutV1Geometry.Background);
                for (var x = left; x < left + width; x++)
                    row[x] = color;
            }
        });

        return Png(image);
    }

    private static byte[] Blank(int width, int height)
    {
        using var image = new Image<L8>(width, height, new L8(LayoutV1Geometry.Background));
        return Png(image);
    }

    private static byte[] Png(Image<L8> image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
