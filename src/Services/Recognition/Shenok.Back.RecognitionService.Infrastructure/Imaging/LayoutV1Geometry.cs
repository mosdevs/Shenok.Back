using WaveformBits = Shenok.Back.WaveformCodec.WaveformCodec;

namespace Shenok.Back.RecognitionService.Infrastructure.Imaging;

/// <summary>
/// The only description of a layout version 1 card. The renderer and the decoder both call this type.
/// Origin is the top-left. X grows right. Y grows down. Bars grow up from the baseline.
/// </summary>
internal static class LayoutV1Geometry
{
    public const int QuietZone = 4;
    public const int AnchorOrigin = 4;
    public const int AnchorSize = 24;
    public const int GapAfterAnchor = 4;
    public const int BarLaneLeft = 32;
    public const int BarWidth = 2;
    public const int BarGap = 1;
    public const int BarPitch = 3;
    public const int BarCount = 24;
    public const int BarLaneHeight = 32;
    public const int BaselineY = 36;
    public const int CardWidth = 107;
    public const int CardHeight = 40;
    public const int CornerRadius = 1;
    public const byte Ink = 0;
    public const byte Background = 255;
    public const byte Decorative = 180;
    public const int InkThreshold = 64;

    public static readonly (double X, double Y) AnchorTopLeft = (AnchorOrigin, AnchorOrigin);
    public static readonly (double X, double Y) AnchorTopRight = (AnchorOrigin + AnchorSize, AnchorOrigin);
    public static readonly (double X, double Y) AnchorBottomRight = (AnchorOrigin + AnchorSize, AnchorOrigin + AnchorSize);
    public static readonly (double X, double Y) AnchorBottomLeft = (AnchorOrigin, AnchorOrigin + AnchorSize);

    static LayoutV1Geometry()
    {
        if (BarCount != WaveformBits.BarCount)
            throw new InvalidOperationException("Layout version 1 bar count must equal the codec bar count.");

        var contentRight = BarLaneLeft + (BarCount * BarWidth) + ((BarCount - 1) * BarGap);
        if (contentRight + QuietZone != CardWidth)
            throw new InvalidOperationException("Layout version 1 card width does not match the bar lane.");
        if (BaselineY + QuietZone != CardHeight)
            throw new InvalidOperationException("Layout version 1 card height does not match the baseline.");
        if (AnchorOrigin + AnchorSize + GapAfterAnchor != BarLaneLeft)
            throw new InvalidOperationException("Layout version 1 bar lane does not start after the anchor.");
    }

    public static int BarHeight(int level)
    {
        if (level is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(level), level, "A bar level is 0 through 7.");

        return (level + 1) * 4;
    }

    public static double BarCenterX(int index)
    {
        if (index < 0 || index >= BarCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return BarLaneLeft + (index * BarPitch) + 1;
    }

    /// <summary>
    /// Anchor mask in local coordinates 0..23. The pip at (16,18)..(17,19) breaks 180° symmetry.
    /// </summary>
    public static bool IsAnchorInk(int x, int y)
    {
        if ((uint)x >= AnchorSize || (uint)y >= AnchorSize)
            return false;

        var ring = Math.Min(Math.Min(x, y), Math.Min(AnchorSize - 1 - x, AnchorSize - 1 - y));
        if (ring <= 2)
            return true;
        if (x is >= 16 and <= 17 && y is >= 18 and <= 19)
            return true;
        if (ring <= 5)
            return false;

        return true;
    }

    public static bool IsBarInk(int x, int y, ReadOnlySpan<byte> levels)
    {
        if ((uint)x >= CardWidth || (uint)y >= CardHeight || levels.Length != BarCount)
            return false;

        if (x < BarLaneLeft)
            return false;

        var offset = x - BarLaneLeft;
        var index = offset / BarPitch;
        var within = offset % BarPitch;
        if (index >= BarCount || within >= BarWidth)
            return false;

        var level = levels[index];
        if (level > 7)
            return false;

        var top = BaselineY - BarHeight(level);
        if (y < top || y >= BaselineY)
            return false;

        return InsideRoundedBar(x + 0.5, y + 0.5, BarLaneLeft + (index * BarPitch), top);
    }

    public static bool ContainsInk(double x, double y, ReadOnlySpan<byte> levels)
    {
        if (x < 0 || y < 0 || x >= CardWidth || y >= CardHeight || levels.Length != BarCount)
            return false;

        if (x >= AnchorOrigin && x < AnchorOrigin + AnchorSize && y >= AnchorOrigin && y < AnchorOrigin + AnchorSize)
            return IsAnchorInk((int)Math.Floor(x) - AnchorOrigin, (int)Math.Floor(y) - AnchorOrigin);

        if (x < BarLaneLeft)
            return false;

        var offset = x - BarLaneLeft;
        var index = (int)Math.Floor(offset / BarPitch);
        if (index < 0 || index >= BarCount)
            return false;

        var level = levels[index];
        if (level > 7)
            return false;

        var left = BarLaneLeft + (index * BarPitch);
        var top = BaselineY - BarHeight(level);
        return InsideRoundedBar(x, y, left, top);
    }

    private static bool InsideRoundedBar(double x, double y, double left, double top)
    {
        var barRight = left + BarWidth;
        var bottom = (double)BaselineY;
        if (x < left || x >= barRight || y < top || y >= bottom)
            return false;

        var innerLeft = left + CornerRadius;
        var innerRight = barRight - CornerRadius;
        var innerTop = top + CornerRadius;
        var innerBottom = bottom - CornerRadius;
        var nearestX = Math.Clamp(x, innerLeft, innerRight);
        var nearestY = Math.Clamp(y, innerTop, innerBottom);
        var dx = x - nearestX;
        var dy = y - nearestY;
        return (dx * dx) + (dy * dy) <= (CornerRadius * CornerRadius);
    }
}
