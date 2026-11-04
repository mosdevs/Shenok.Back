namespace Shenok.Back.WaveformCodec;

internal static class ReedSolomon16
{
    private const int CorrectionCapacity = 3;

    public static byte[] Encode(byte[] data, int paritySymbols)
    {
        var generator = Generator(paritySymbols);
        var result = new byte[data.Length + paritySymbols];
        Array.Copy(data, result, data.Length);
        for (var i = 0; i < data.Length; i++)
        {
            var coefficient = result[i];
            if (coefficient == 0)
                continue;

            for (var j = 1; j < generator.Length; j++)
                result[i + j] ^= Gf16.Mul(generator[j], coefficient);
        }

        Array.Copy(data, result, data.Length);
        return result;
    }

    public static bool TryDecode(byte[] received, int paritySymbols, out byte[] corrected)
    {
        corrected = [];
        if (IsCodeword(received, paritySymbols))
        {
            corrected = received.ToArray();
            return true;
        }

        var candidate = received.ToArray();
        if (TryCorrect(candidate, paritySymbols, 0, CorrectionCapacity))
        {
            corrected = candidate;
            return true;
        }

        return false;
    }

    private static bool TryCorrect(byte[] candidate, int paritySymbols, int start, int remaining)
    {
        if (IsCodeword(candidate, paritySymbols))
            return true;
        if (remaining == 0)
            return false;

        for (var index = start; index < candidate.Length; index++)
        {
            var original = candidate[index];
            for (byte value = 0; value < 16; value++)
            {
                if (value == original)
                    continue;

                candidate[index] = value;
                if (TryCorrect(candidate, paritySymbols, index + 1, remaining - 1))
                    return true;
            }

            candidate[index] = original;
        }

        return false;
    }

    private static bool IsCodeword(byte[] word, int paritySymbols)
    {
        var encoded = Encode(word[..^paritySymbols], paritySymbols);
        return encoded.AsSpan().SequenceEqual(word);
    }

    private static byte[] Generator(int paritySymbols)
    {
        byte[] generator = [1];
        for (var i = 0; i < paritySymbols; i++)
        {
            var next = new byte[generator.Length + 1];
            for (var j = 0; j < generator.Length; j++)
            {
                next[j] ^= generator[j];
                next[j + 1] ^= Gf16.Mul(generator[j], Gf16.Exp(i));
            }

            generator = next;
        }

        return generator;
    }
}

internal static class Gf16
{
    private const int Primitive = 0x13;
    private static readonly byte[] ExpTable = new byte[30];
    private static readonly byte[] LogTable = new byte[16];

    static Gf16()
    {
        byte value = 1;
        for (var i = 0; i < 15; i++)
        {
            ExpTable[i] = value;
            LogTable[value] = (byte)i;
            value <<= 1;
            if ((value & 0x10) != 0)
                value ^= Primitive;
        }

        for (var i = 15; i < ExpTable.Length; i++)
            ExpTable[i] = ExpTable[i - 15];
    }

    public static byte Exp(int power) => ExpTable[power % 15];

    public static byte Mul(byte left, byte right)
    {
        if (left == 0 || right == 0)
            return 0;
        return ExpTable[LogTable[left] + LogTable[right]];
    }
}
