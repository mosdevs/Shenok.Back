using Shenok.Back.WaveformCodec;

namespace Shenok.Back.WaveformCodec.Tests;

public class WaveformCodecTests
{
    [Theory]
    [InlineData(1ul)]
    [InlineData(42ul)]
    [InlineData(1_048_576ul)]
    [InlineData((1ul << 40) - 1)]
    public void Round_trip_returns_the_same_code(ulong code)
    {
        var bars = WaveformCodec.Encode(code);
        Assert.Equal(WaveformCodec.BarCount, bars.Length);
        Assert.True(WaveformCodec.TryDecode(bars, out var decoded));
        Assert.Equal(code, decoded);
    }

    [Fact]
    public void A_single_symbol_error_corrects_back_to_the_code()
    {
        var bars = WaveformCodec.Encode(123_456);
        var broken = bars.ToArray();
        broken[3] = (byte)((broken[3] + 1) & 7);
        Assert.True(WaveformCodec.TryDecode(broken, out var decoded));
        Assert.Equal(123_456ul, decoded);
    }

    [Fact]
    public void Damage_past_the_correction_budget_is_not_a_nearby_code()
    {
        var bars = WaveformCodec.Encode(987_654);
        var broken = bars.ToArray();
        for (var i = 0; i < broken.Length; i += 2)
            broken[i] = (byte)((broken[i] + 3) & 7);

        var decoded = WaveformCodec.TryDecode(broken, out var code);
        Assert.True(!decoded || code == 987_654);
        Assert.False(decoded);
    }
}
