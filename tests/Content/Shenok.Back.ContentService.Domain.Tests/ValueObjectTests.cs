using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Tests;

public class ValueObjectTests
{
    [Fact]
    public void PageTitle_trims_and_rejects_blank_or_oversized_values()
    {
        Assert.Equal("Show", PageTitle.Create("  Show  ").Value);
        Assert.Throws<InvalidPageTitleException>(() => PageTitle.Create(" "));
        Assert.Throws<InvalidPageTitleException>(() => PageTitle.Create(new string('a', PageTitle.MaxLength + 1)));
    }

    [Fact]
    public void PageDescription_allows_blank_and_rejects_oversized_values()
    {
        Assert.Equal(string.Empty, PageDescription.Create(null).Value);
        Assert.Equal(string.Empty, PageDescription.Create("   ").Value);
        Assert.Equal("Hello", PageDescription.Create("  Hello  ").Value);
        Assert.Throws<InvalidPageDescriptionException>(() =>
            PageDescription.Create(new string('d', PageDescription.MaxLength + 1)));
    }

    [Fact]
    public void CoverImageUrl_requires_an_absolute_http_or_https_url()
    {
        Assert.Null(CoverImageUrl.Create(null));
        Assert.Null(CoverImageUrl.Create(" "));
        Assert.Equal("https://cdn.example.com/a.jpg", CoverImageUrl.Create("  https://cdn.example.com/a.jpg  ")!.Value);
        Assert.Throws<InvalidCoverImageUrlException>(() => CoverImageUrl.Create("ftp://cdn.example.com/a.jpg"));
        Assert.Throws<InvalidCoverImageUrlException>(() => CoverImageUrl.Create("/relative.jpg"));
        Assert.Throws<InvalidCoverImageUrlException>(() =>
            CoverImageUrl.Create("https://cdn.example.com/" + new string('x', CoverImageUrl.MaxLength)));
    }

    [Fact]
    public void VoiceTitle_trims_and_rejects_blank_values()
    {
        Assert.Equal("Ep 1", VoiceTitle.Create("  Ep 1  ").Value);
        Assert.Throws<InvalidVoiceTitleException>(() => VoiceTitle.Create(""));
    }

    [Fact]
    public void WaveformCode_must_be_a_positive_40_bit_integer()
    {
        Assert.Equal(1, WaveformCode.From(1).Value);
        Assert.Equal(WaveformCode.MaxValue, WaveformCode.From(WaveformCode.MaxValue).Value);
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(0));
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(-1));
        Assert.Throws<InvalidWaveformCodeException>(() => WaveformCode.From(WaveformCode.MaxValue + 1));
    }

    [Fact]
    public void LayoutVersion_only_allows_version_1()
    {
        Assert.Equal(1, LayoutVersion.From(1).Value);
        Assert.Same(LayoutVersion.V1, LayoutVersion.From(1));
        Assert.Throws<InvalidLayoutVersionException>(() => LayoutVersion.From(0));
        Assert.Throws<InvalidLayoutVersionException>(() => LayoutVersion.From(2));
    }

    [Fact]
    public void Typed_ids_reject_an_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => PageId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => OwnerId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => VoiceId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => WaveformId.From(Guid.Empty));

        var guid = Guid.NewGuid();
        Assert.Equal(guid, PageId.From(guid).Value);
        Assert.NotEqual(Guid.Empty, PageId.New().Value);
    }
}
