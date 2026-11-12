namespace Shenok.Back.WaveformCodec;

public static class WaveformCodec
{
    public const int LayoutVersion = 1;
    public const int BarCount = 24;
    public const int BitsPerBar = 3;
    public const int DataNibbles = 11;
    public const int ParityNibbles = 7;
    public const int CodeBits = 40;

    public static byte[] Encode(ulong code)
    {
        if (code == 0 || code >= 1UL << CodeBits)
            throw new ArgumentOutOfRangeException(nameof(code), "Waveform code must be a positive integer of at most 40 bits.");

        var data = new byte[DataNibbles];
        data[0] = LayoutVersion;
        for (var i = 0; i < 10; i++)
            data[1 + i] = (byte)((code >> (4 * (9 - i))) & 0xF);

        var codeword = ReedSolomon16.Encode(data, ParityNibbles);
        return NibblesToBars(codeword);
    }

    public static bool TryDecode(ReadOnlySpan<byte> bars, out ulong code)
    {
        code = 0;
        if (bars.Length != BarCount || bars.ToArray().Any(bar => bar > 7))
            return false;

        var nibbles = BarsToNibbles(bars);
        // #region agent log
        try
        {
            var clean = Encode(123_456);
            var cleanNibbles = BarsToNibbles(clean);
            var distance = 0;
            for (var i = 0; i < nibbles.Length; i++)
                if (nibbles[i] != cleanNibbles[i]) distance++;
            var line = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = "695b6c",
                hypothesisId = "A",
                location = "WaveformCodec.cs:TryDecode",
                message = "nibble-distance-from-123456",
                data = new { bars = bars.ToArray(), nibbles, cleanNibbles, distance },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            System.IO.File.AppendAllText(@"d:\Shenok\.cursor\debug-695b6c.log", line + "\n");
        }
        catch { }
        // #endregion
        if (!ReedSolomon16.TryDecode(nibbles, ParityNibbles, out var corrected))
            return false;

        if (corrected[0] != LayoutVersion)
            return false;

        ulong value = 0;
        for (var i = 0; i < 10; i++)
            value = (value << 4) | corrected[1 + i];

        if (value == 0 || value >= 1UL << CodeBits)
            return false;

        code = value;
        return true;
    }

    private static byte[] NibblesToBars(byte[] nibbles)
    {
        var bars = new byte[BarCount];
        var bitIndex = 0;
        foreach (var nibble in nibbles)
        {
            for (var shift = 3; shift >= 0; shift--)
            {
                var bit = (nibble >> shift) & 1;
                var bar = bitIndex / BitsPerBar;
                var barShift = 2 - (bitIndex % BitsPerBar);
                bars[bar] |= (byte)(bit << barShift);
                bitIndex++;
            }
        }

        return bars;
    }

    private static byte[] BarsToNibbles(ReadOnlySpan<byte> bars)
    {
        var nibbles = new byte[DataNibbles + ParityNibbles];
        var bitIndex = 0;
        for (var nibble = 0; nibble < nibbles.Length; nibble++)
        {
            byte value = 0;
            for (var shift = 3; shift >= 0; shift--)
            {
                var bar = bars[bitIndex / BitsPerBar];
                var barShift = 2 - (bitIndex % BitsPerBar);
                value |= (byte)(((bar >> barShift) & 1) << shift);
                bitIndex++;
            }

            nibbles[nibble] = value;
        }

        return nibbles;
    }
}
