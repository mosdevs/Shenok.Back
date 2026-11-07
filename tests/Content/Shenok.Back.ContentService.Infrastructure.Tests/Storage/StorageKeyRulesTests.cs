using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Storage;

public sealed class StorageKeyRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("sources")]
    [InlineData("/sources/a")]
    [InlineData("sources/a/")]
    [InlineData("sources//a")]
    [InlineData("sources/../a")]
    [InlineData("sources/..")]
    [InlineData("sources\\a")]
    [InlineData("sources/a:b")]
    [InlineData("http://evil/a")]
    [InlineData("sources/a b")]
    [InlineData("sources/a\nb")]
    [InlineData("sources/موج")]
    public void Rejects_keys_that_can_escape_the_container(string? key)
    {
        Assert.False(StorageKeyRules.IsObjectKey(key));
        var exception = Assert.Throws<ArgumentException>(() => StorageKeyRules.RequireObjectKey(key!));
        Assert.Equal("storageKey", exception.ParamName);
    }

    [Theory]
    [InlineData("sources/a.wav")]
    [InlineData("packages/abc/index.m3u8")]
    [InlineData("packages/abc/seg_00000.ts")]
    public void Accepts_a_relative_object_key(string key)
    {
        Assert.True(StorageKeyRules.IsObjectKey(key));
        StorageKeyRules.RequireObjectKey(key);
    }

    [Fact]
    public void Rejects_a_key_longer_than_the_domain_limit_and_accepts_the_limit()
    {
        var tooLong = "sources/" + new string('a', AudioAsset.MaxKeyLength);
        var atLimit = "s/" + new string('a', AudioAsset.MaxKeyLength - 2);

        Assert.Equal(AudioAsset.MaxKeyLength, atLimit.Length);
        Assert.False(StorageKeyRules.IsObjectKey(tooLong));
        Assert.True(StorageKeyRules.IsObjectKey(atLimit));
    }
}
