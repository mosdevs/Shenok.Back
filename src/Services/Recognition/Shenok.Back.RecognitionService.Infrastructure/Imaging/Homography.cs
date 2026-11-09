namespace Shenok.Back.RecognitionService.Infrastructure.Imaging;

/// <summary>
/// Maps card-module coordinates to image pixels. Eight coefficients, with the last fixed at 1.
/// </summary>
internal readonly struct Homography
{
    private readonly double _h11;
    private readonly double _h12;
    private readonly double _h13;
    private readonly double _h21;
    private readonly double _h22;
    private readonly double _h23;
    private readonly double _h31;
    private readonly double _h32;

    private Homography(
        double h11, double h12, double h13,
        double h21, double h22, double h23,
        double h31, double h32)
    {
        _h11 = h11;
        _h12 = h12;
        _h13 = h13;
        _h21 = h21;
        _h22 = h22;
        _h23 = h23;
        _h31 = h31;
        _h32 = h32;
    }

    public static bool TrySolve(
        (double X, double Y) topLeft,
        (double X, double Y) topRight,
        (double X, double Y) bottomRight,
        (double X, double Y) bottomLeft,
        out Homography homography)
    {
        // Canonical anchor corners, in the same order as the image corners.
        Span<(double X, double Y, double U, double V)> pairs =
        [
            (LayoutV1Geometry.AnchorTopLeft.X, LayoutV1Geometry.AnchorTopLeft.Y, topLeft.X, topLeft.Y),
            (LayoutV1Geometry.AnchorTopRight.X, LayoutV1Geometry.AnchorTopRight.Y, topRight.X, topRight.Y),
            (LayoutV1Geometry.AnchorBottomRight.X, LayoutV1Geometry.AnchorBottomRight.Y, bottomRight.X, bottomRight.Y),
            (LayoutV1Geometry.AnchorBottomLeft.X, LayoutV1Geometry.AnchorBottomLeft.Y, bottomLeft.X, bottomLeft.Y)
        ];

        var matrix = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            var (x, y, u, v) = pairs[i];
            var row = i * 2;
            matrix[row, 0] = x;
            matrix[row, 1] = y;
            matrix[row, 2] = 1;
            matrix[row, 6] = -u * x;
            matrix[row, 7] = -u * y;
            matrix[row, 8] = u;
            matrix[row + 1, 3] = x;
            matrix[row + 1, 4] = y;
            matrix[row + 1, 5] = 1;
            matrix[row + 1, 6] = -v * x;
            matrix[row + 1, 7] = -v * y;
            matrix[row + 1, 8] = v;
        }

        Span<double> solution = stackalloc double[8];
        if (!Solve(matrix, solution))
        {
            homography = default;
            return false;
        }

        homography = new Homography(
            solution[0], solution[1], solution[2],
            solution[3], solution[4], solution[5],
            solution[6], solution[7]);
        return true;
    }

    public (double X, double Y) Map(double x, double y)
    {
        var weight = (_h31 * x) + (_h32 * y) + 1;
        if (Math.Abs(weight) < 1e-12)
            return (double.NaN, double.NaN);

        return (((_h11 * x) + (_h12 * y) + _h13) / weight, ((_h21 * x) + (_h22 * y) + _h23) / weight);
    }

    private static bool Solve(double[,] matrix, Span<double> solution)
    {
        const int size = 8;
        for (var column = 0; column < size; column++)
        {
            var pivot = column;
            var pivotMagnitude = Math.Abs(matrix[column, column]);
            for (var row = column + 1; row < size; row++)
            {
                var magnitude = Math.Abs(matrix[row, column]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivot = row;
                }
            }

            if (pivotMagnitude < 1e-10)
                return false;

            if (pivot != column)
            {
                for (var index = column; index <= size; index++)
                    (matrix[column, index], matrix[pivot, index]) = (matrix[pivot, index], matrix[column, index]);
            }

            var scale = matrix[column, column];
            for (var index = column; index <= size; index++)
                matrix[column, index] /= scale;

            for (var row = 0; row < size; row++)
            {
                if (row == column)
                    continue;

                var factor = matrix[row, column];
                if (factor == 0)
                    continue;

                for (var index = column; index <= size; index++)
                    matrix[row, index] -= factor * matrix[column, index];
            }
        }

        for (var index = 0; index < size; index++)
            solution[index] = matrix[index, size];

        return true;
    }
}
