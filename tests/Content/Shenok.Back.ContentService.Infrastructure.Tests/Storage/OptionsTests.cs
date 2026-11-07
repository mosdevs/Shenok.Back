using Shenok.Back.ContentService.Infrastructure.Packaging;
using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Storage;

public sealed class OptionsTests
{
    [Theory]
    [InlineData("content", true)]
    [InlineData("abc", true)]
    [InlineData("3content", true)]
    [InlineData("ab", false)]
    [InlineData("-content", false)]
    [InlineData("content-", false)]
    [InlineData("con--tent", false)]
    [InlineData("Content", false)]
    [InlineData("content_1", false)]
    [InlineData("content.audio", false)]
    public void Blob_container_name_follows_the_private_container_rules(string name, bool valid)
    {
        Assert.Equal(valid, BlobStorageOptions.IsValid(new BlobStorageOptions { ContainerName = name }));
    }

    [Fact]
    public void Blob_container_name_accepts_63_characters_and_rejects_64()
    {
        Assert.True(BlobStorageOptions.IsValid(new BlobStorageOptions { ContainerName = new string('a', 63) }));
        Assert.False(BlobStorageOptions.IsValid(new BlobStorageOptions { ContainerName = new string('a', 64) }));
    }

    [Fact]
    public void Default_blob_container_is_valid()
    {
        Assert.True(BlobStorageOptions.IsValid(new BlobStorageOptions()));
        Assert.Equal("content", new BlobStorageOptions().ContainerName);
    }

    [Theory]
    [InlineData("ffmpeg & calc")]
    [InlineData("ffmpeg | calc")]
    [InlineData("ffmpeg;calc")]
    [InlineData("ffmpeg`calc")]
    [InlineData("$(calc)")]
    [InlineData("ffmpeg > out")]
    [InlineData("ffmpeg < in")]
    [InlineData("ffmpeg\"x")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData(" ")]
    public void Packaging_tool_path_rejects_shell_metacharacters(string path)
    {
        var options = ValidPackaging();
        options.FfmpegPath = path;
        Assert.False(PackagingOptions.IsValid(options));
    }

    [Fact]
    public void Packaging_accepts_a_windows_tool_path_and_bounds_the_worker()
    {
        var options = ValidPackaging();
        options.FfmpegPath = """C:\Program Files\ffmpeg\ffmpeg.exe""";
        options.FfprobePath = "ffprobe";
        Assert.True(PackagingOptions.IsValid(options));

        options.ProcessTimeoutSeconds = 29;
        Assert.False(PackagingOptions.IsValid(options));
        options.ProcessTimeoutSeconds = 30;
        Assert.True(PackagingOptions.IsValid(options));
        options.ProcessTimeoutSeconds = 3601;
        Assert.False(PackagingOptions.IsValid(options));

        options = ValidPackaging();
        options.PollSeconds = 0;
        Assert.False(PackagingOptions.IsValid(options));
        options.PollSeconds = 61;
        Assert.False(PackagingOptions.IsValid(options));

        options = ValidPackaging();
        options.BatchSize = 0;
        Assert.False(PackagingOptions.IsValid(options));
        options.BatchSize = 9;
        Assert.False(PackagingOptions.IsValid(options));
        options.BatchSize = 8;
        Assert.True(PackagingOptions.IsValid(options));
    }

    [Fact]
    public void Packaging_rejects_a_control_character_and_an_overlong_path()
    {
        var options = ValidPackaging();
        options.FfprobePath = "ffprobe\n-i";
        Assert.False(PackagingOptions.IsValid(options));

        options = ValidPackaging();
        options.FfmpegPath = new string('a', 261);
        Assert.False(PackagingOptions.IsValid(options));
    }

    private static PackagingOptions ValidPackaging() => new();
}
