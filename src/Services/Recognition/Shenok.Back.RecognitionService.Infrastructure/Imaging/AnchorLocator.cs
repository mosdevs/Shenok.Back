namespace Shenok.Back.RecognitionService.Infrastructure.Imaging;

/// <summary>
/// Finds the layout version 1 anchor and the four corners of its frame.
/// The search is a multi-scale binary template match. The corners are the outer frame, not the axis-aligned box,
/// so a mild trapezoid still fixes a homography.
/// </summary>
internal static class AnchorLocator
{
    private const double AcceptScore = 0.75;
    private const int Template = LayoutV1Geometry.AnchorSize;

    public readonly record struct Square(double Left, double Top, double Size, double Score);

    public static bool TryFind(
        ReadOnlySpan<byte> binary,
        int width,
        int height,
        ReadOnlySpan<byte> luminance,
        int originalWidth,
        int originalHeight,
        CancellationToken cancellationToken,
        out (double X, double Y) topLeft,
        out (double X, double Y) topRight,
        out (double X, double Y) bottomRight,
        out (double X, double Y) bottomLeft)
    {
        topLeft = topRight = bottomRight = bottomLeft = default;
        var match = Search(binary, width, height, cancellationToken);
        if (match is null)
            return false;

        var scaleX = originalWidth / (double)width;
        var scaleY = originalHeight / (double)height;
        var left = match.Value.Left * scaleX;
        var top = match.Value.Top * scaleY;
        var size = match.Value.Size * scaleX;

        if (!TryRefine(luminance, originalWidth, originalHeight, left, top, size,
                out topLeft, out topRight, out bottomRight, out bottomLeft))
        {
            topLeft = (left, top);
            topRight = (left + size, top);
            bottomRight = (left + size, top + (size * scaleY / scaleX));
            bottomLeft = (left, top + (size * scaleY / scaleX));
        }

        return true;
    }

    private static Square? Search(ReadOnlySpan<byte> binary, int width, int height, CancellationToken cancellationToken)
    {
        var bestScore = AcceptScore;
        Square? best = null;
        var max = Math.Min(width, height);
        for (var size = Template; size <= max;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stride = Math.Max(1, size / Template);
            if (TryBest(binary, width, height, size, stride, bestScore, out var candidate) && candidate.Score >= bestScore)
            {
                bestScore = candidate.Score;
                best = candidate;
                if (candidate.Score >= 0.98)
                    break;
            }

            var next = Math.Max(size + 1, (int)Math.Round(size * 1.08));
            size = next == size ? size + 1 : next;
        }

        if (best is not { } found)
            return null;

        return RefineScore(binary, width, height, found, cancellationToken);
    }

    private static Square RefineScore(
        ReadOnlySpan<byte> binary,
        int width,
        int height,
        Square found,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var size = (int)Math.Round(found.Size);
        var window = Math.Max(2, size / Template);
        var best = found;
        var sizeMin = Math.Max(Template, size - Math.Max(2, size / 16));
        var sizeMax = Math.Min(Math.Min(width, height), size + Math.Max(2, size / 16));
        var left0 = (int)Math.Round(found.Left);
        var top0 = (int)Math.Round(found.Top);
        for (var candidateSize = sizeMin; candidateSize <= sizeMax; candidateSize++)
        {
            for (var top = top0 - window; top <= top0 + window; top++)
            {
                for (var left = left0 - window; left <= left0 + window; left++)
                {
                    if (!TryScore(binary, width, height, left, top, candidateSize, out var score))
                        continue;
                    if (score > best.Score)
                        best = new Square(left, top, candidateSize, score);
                }
            }
        }

        return best.Score >= AcceptScore ? best : found;
    }

    private static bool TryBest(
        ReadOnlySpan<byte> binary,
        int width,
        int height,
        int size,
        int stride,
        double threshold,
        out Square best)
    {
        best = default;
        var found = false;
        for (var top = 0; top <= height - size; top += stride)
        {
            for (var left = 0; left <= width - size; left += stride)
            {
                if (!TryScore(binary, width, height, left, top, size, out var score) || score < threshold)
                    continue;

                if (!found || score > best.Score)
                {
                    best = new Square(left, top, size, score);
                    found = true;
                    threshold = score;
                }
            }
        }

        return found;
    }

    private static bool TryScore(ReadOnlySpan<byte> binary, int width, int height, int left, int top, int size, out double score)
    {
        score = 0;
        if (left < 0 || top < 0 || size < Template || left + size > width || top + size > height)
            return false;
        if (!ProbesMatch(binary, width, left, top, size))
            return false;

        var matches = 0;
        for (var ty = 0; ty < Template; ty++)
        {
            var y = top + ((ty * size) + (size / 2)) / Template;
            var row = y * width;
            for (var tx = 0; tx < Template; tx++)
            {
                var x = left + ((tx * size) + (size / 2)) / Template;
                var ink = binary[row + x] != 0;
                if (ink == LayoutV1Geometry.IsAnchorInk(tx, ty))
                    matches++;
            }
        }

        score = matches / (double)(Template * Template);
        return true;
    }

    private static bool ProbesMatch(ReadOnlySpan<byte> binary, int width, int left, int top, int size)
    {
        // Outer frame and the inner block are black. The white ring is the negative probe.
        ReadOnlySpan<(int X, int Y, bool Ink)> probes =
        [
            (1, 1, true), (12, 1, true), (22, 1, true),
            (1, 12, true), (22, 12, true), (12, 12, true),
            (1, 22, true), (12, 22, true), (22, 22, true),
            (4, 8, false), (8, 4, false), (19, 8, false)
        ];

        foreach (var (x, y, ink) in probes)
        {
            var sampleX = left + ((x * size) + (size / 2)) / Template;
            var sampleY = top + ((y * size) + (size / 2)) / Template;
            var actual = binary[(sampleY * width) + sampleX] != 0;
            if (actual != ink)
                return false;
        }

        return true;
    }

    private static bool TryRefine(
        ReadOnlySpan<byte> luminance,
        int width,
        int height,
        double left,
        double top,
        double size,
        out (double X, double Y) topLeft,
        out (double X, double Y) topRight,
        out (double X, double Y) bottomRight,
        out (double X, double Y) bottomLeft)
    {
        topLeft = topRight = bottomRight = bottomLeft = default;
        // Stay inside the four-module gap. A wider search walks into the bar lane and fits the wrong edge.
        var margin = Math.Max(4, size / 16.0);
        if (!TryFitHorizontal(luminance, width, height, left, top, size, margin, outsideIsAbove: true, out var topLine)
            || !TryFitHorizontal(luminance, width, height, left, top + size, size, margin, outsideIsAbove: false, out var bottomLine)
            || !TryFitVertical(luminance, width, height, left, top, size, margin, outsideIsLeft: true, out var leftLine)
            || !TryFitVertical(luminance, width, height, left + size, top, size, margin, outsideIsLeft: false, out var rightLine))
        {
            return false;
        }

        return TryIntersect(topLine, leftLine, out topLeft)
            && TryIntersect(topLine, rightLine, out topRight)
            && TryIntersect(bottomLine, rightLine, out bottomRight)
            && TryIntersect(bottomLine, leftLine, out bottomLeft);
    }

    private readonly record struct Line(double A, double B, double C);

    private static bool TryFitHorizontal(
        ReadOnlySpan<byte> luminance,
        int width,
        int height,
        double left,
        double edgeY,
        double size,
        double margin,
        bool outsideIsAbove,
        out Line line)
    {
        Span<(double X, double Y)> points = stackalloc (double X, double Y)[7];
        var count = 0;
        for (var step = 2; step <= 8; step++)
        {
            var x = left + (size * step / 10.0);
            var y0 = outsideIsAbove ? edgeY - margin : edgeY + margin;
            var y1 = outsideIsAbove ? edgeY + margin : edgeY - margin;
            if (!TryFirstInk(luminance, width, height, x, y0, x, y1, out var hitX, out var hitY))
                continue;

            points[count++] = (hitX, hitY);
        }

        return TryFit(points[..count], horizontal: true, out line);
    }

    private static bool TryFitVertical(
        ReadOnlySpan<byte> luminance,
        int width,
        int height,
        double edgeX,
        double top,
        double size,
        double margin,
        bool outsideIsLeft,
        out Line line)
    {
        Span<(double X, double Y)> points = stackalloc (double X, double Y)[7];
        var count = 0;
        for (var step = 2; step <= 8; step++)
        {
            var y = top + (size * step / 10.0);
            var x0 = outsideIsLeft ? edgeX - margin : edgeX + margin;
            var x1 = outsideIsLeft ? edgeX + margin : edgeX - margin;
            if (!TryFirstInk(luminance, width, height, x0, y, x1, y, out var hitX, out var hitY))
                continue;

            points[count++] = (hitX, hitY);
        }

        return TryFit(points[..count], horizontal: false, out line);
    }

    private static bool TryFirstInk(
        ReadOnlySpan<byte> luminance,
        int width,
        int height,
        double x0,
        double y0,
        double x1,
        double y1,
        out double x,
        out double y)
    {
        const int samples = 48;
        for (var step = 0; step <= samples; step++)
        {
            var t = step / (double)samples;
            var sx = x0 + ((x1 - x0) * t);
            var sy = y0 + ((y1 - y0) * t);
            var ix = (int)Math.Round(sx);
            var iy = (int)Math.Round(sy);
            if ((uint)ix >= (uint)width || (uint)iy >= (uint)height)
                continue;
            if (luminance[(iy * width) + ix] < LayoutV1Geometry.InkThreshold)
            {
                x = sx;
                y = sy;
                return true;
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool TryFit(ReadOnlySpan<(double X, double Y)> points, bool horizontal, out Line line)
    {
        line = default;
        if (points.Length < 4)
            return false;

        double sumX = 0, sumY = 0, sumXX = 0, sumXY = 0;
        foreach (var (x, y) in points)
        {
            sumX += x;
            sumY += y;
            sumXX += x * x;
            sumXY += x * y;
        }

        var n = points.Length;
        if (horizontal)
        {
            var denominator = (n * sumXX) - (sumX * sumX);
            if (Math.Abs(denominator) < 1e-6)
                return false;

            var slope = ((n * sumXY) - (sumX * sumY)) / denominator;
            var intercept = (sumY - (slope * sumX)) / n;
            line = new Line(slope, -1, -intercept);
            return true;
        }

        double sumYY = 0, sumYX = 0;
        foreach (var (x, y) in points)
        {
            sumYY += y * y;
            sumYX += y * x;
        }

        var verticalDenominator = (n * sumYY) - (sumY * sumY);
        if (Math.Abs(verticalDenominator) < 1e-6)
            return false;

        var verticalSlope = ((n * sumYX) - (sumY * sumX)) / verticalDenominator;
        var verticalIntercept = (sumX - (verticalSlope * sumY)) / n;
        line = new Line(-1, verticalSlope, -verticalIntercept);
        return true;
    }

    private static bool TryIntersect(Line first, Line second, out (double X, double Y) point)
    {
        var determinant = (first.A * second.B) - (second.A * first.B);
        if (Math.Abs(determinant) < 1e-8)
        {
            point = default;
            return false;
        }

        point = (
            ((first.C * second.B) - (second.C * first.B)) / determinant,
            ((first.A * second.C) - (second.A * first.C)) / determinant);
        return true;
    }
}
