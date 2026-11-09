using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;

namespace Shenok.Back.RecognitionService.Application.Tests;

public class DecodeWaveformValidatorTests
{
    private readonly DecodeWaveformValidator _validator = new();

    [Fact]
    public void Validator_rejects_a_null_image_an_empty_image_an_oversize_image_and_a_content_type_that_is_not_jpeg_or_png()
    {
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/jpeg", null!)).IsValid);
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/jpeg", [])).IsValid);
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/jpeg", new byte[DecodeWaveformValidator.MaxImageBytes + 1])).IsValid);
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/gif", [1])).IsValid);
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/jpeg; charset=binary", [1])).IsValid);
        Assert.False(_validator.Validate(new DecodeWaveformRequest("image/jpg", [1])).IsValid);
    }

    [Fact]
    public void Validator_accepts_jpeg_and_png_inside_the_size_window()
    {
        Assert.True(_validator.Validate(new DecodeWaveformRequest("image/jpeg", [1])).IsValid);
        Assert.True(_validator.Validate(new DecodeWaveformRequest("IMAGE/PNG", [1])).IsValid);
        Assert.True(_validator.Validate(new DecodeWaveformRequest("image/jpeg", new byte[DecodeWaveformValidator.MaxImageBytes])).IsValid);
    }
}
