using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Shenok.Back.RecognitionService.Infrastructure.Imaging;

/// <summary>
/// Renders a layout version 1 card. Tests use this. The decoder does not.
/// </summary>
internal static class LayoutV1Card
{
    public static byte[] Render(ReadOnlySpan<byte> levels, int pixelsPerModule, bool drawDecorativeAudio)
    {
        if (levels.Length != LayoutV1Geometry.BarCount)
            throw new ArgumentException("Layout version 1 needs 24 bar levels.", nameof(levels));
        if (pixelsPerModule < 1)
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule));

        foreach (var level in levels)
        {
            if (level > 7)
                throw new ArgumentOutOfRangeException(nameof(levels), "A bar level is 0 through 7.");
        }

        var width = LayoutV1Geometry.CardWidth * pixelsPerModule;
        var height = LayoutV1Geometry.CardHeight * pixelsPerModule;
        var levelBytes = levels.ToArray();
        using var image = new Image<L8>(width, height, new L8(LayoutV1Geometry.Background));
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var moduleY = (y + 0.5) / pixelsPerModule;
                for (var x = 0; x < width; x++)
                {
                    var moduleX = (x + 0.5) / pixelsPerModule;
                    if (drawDecorativeAudio && IsDecoration(moduleX, moduleY))
                        row[x] = new L8(LayoutV1Geometry.Decorative);
                    if (LayoutV1Geometry.ContainsInk(moduleX, moduleY, levelBytes))
                        row[x] = new L8(LayoutV1Geometry.Ink);
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Mid-gray strokes behind the bars. Luminance 180 is above the ink threshold, so the decoder ignores them.
    /// </summary>
    private static bool IsDecoration(double x, double y)
    {
        if (x < LayoutV1Geometry.BarLaneLeft || y < 8 || y >= LayoutV1Geometry.BaselineY)
            return false;

        var band = (int)Math.Floor(y);
        return band % 6 == 2;
    }
}
