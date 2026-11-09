using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Tests;

public class WaveformIndexValueTests
{
    [Fact]
    public void WaveformCode_rejects_zero_negative_and_values_past_40_bits()
    {
        Assert.Equal(1, WaveformCode.From(1).Value);
        Assert.Equal(WaveformCode.MaxValue, WaveformCode.From(WaveformCode.MaxValue).Value);
        Assert.Equal(WaveformCode.MaxValue, WaveformCode.From((1L << 40) - 1).Value);

        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(0));
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(-1));
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(1L << 40));
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(WaveformCode.MaxValue + 1));
    }

    [Fact]
    public void WaveformId_rejects_an_empty_guid_and_new_does_not()
    {
        Assert.Throws<ArgumentException>(() => WaveformId.From(Guid.Empty));

        var guid = Guid.NewGuid();
        Assert.Equal(guid, WaveformId.From(guid).Value);
        Assert.NotEqual(Guid.Empty, WaveformId.New().Value);
        Assert.Equal(guid.ToString(), WaveformId.From(guid).ToString());
    }

    [Fact]
    public void Default_apply_result_is_not_a_successful_write()
    {
        Assert.NotEqual(IndexApplyResult.Applied, default(IndexApplyResult));
        Assert.NotEqual(IndexApplyOutcome.Applied, default(IndexApplyResult).Outcome);
    }

    [Fact]
    public void LayoutVersion_rejects_anything_below_1_and_keeps_a_later_version()
    {
        Assert.Equal(LayoutVersion.Version1, LayoutVersion.From(1).Value);
        Assert.Equal(2, LayoutVersion.From(2).Value);
        Assert.Throws<InvalidLayoutVersionException>(() => LayoutVersion.From(0));
        Assert.Throws<InvalidLayoutVersionException>(() => LayoutVersion.From(-1));
    }
}
