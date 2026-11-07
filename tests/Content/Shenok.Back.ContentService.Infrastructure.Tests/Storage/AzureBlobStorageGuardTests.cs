using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Storage;

public sealed class AzureBlobStorageGuardTests
{
    [Theory]
    [InlineData("../secret")]
    [InlineData("sources/../../windows/win.ini")]
    [InlineData("sources\\file")]
    [InlineData("http://evil/object")]
    public async Task Put_rejects_an_escaping_key_before_using_the_network(string key)
    {
        var storage = UnconnectedStorage();
        await using var content = new MemoryStream([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.PutAsync(new BlobUpload(key, content, "audio/wav", content.Length), CancellationToken.None));

        Assert.Equal("storageKey", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Put_rejects_a_non_positive_declared_size(long size)
    {
        var storage = UnconnectedStorage();
        await using var content = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            storage.PutAsync(new BlobUpload("sources/a.bin", content, "audio/wav", size), CancellationToken.None));
    }

    [Fact]
    public async Task Put_rejects_a_declared_size_above_100_megabytes()
    {
        var storage = UnconnectedStorage();
        await using var content = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            storage.PutAsync(
                new BlobUpload("sources/a.bin", content, "audio/wav", AudioAsset.MaxSourceSizeBytes + 1),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("audio")]
    [InlineData("audio/wav\r\nX-Injected: yes")]
    [InlineData("audio/wav<script>")]
    [InlineData("text/html")]
    [InlineData("text/html; charset=utf-8")]
    public async Task Put_rejects_a_malformed_content_type(string contentType)
    {
        var storage = UnconnectedStorage();
        await using var content = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.PutAsync(new BlobUpload("sources/a.bin", content, contentType, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Put_rejects_a_missing_upload_or_a_missing_stream()
    {
        var storage = UnconnectedStorage();

        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.PutAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.PutAsync(new BlobUpload("sources/a.bin", null!, "audio/wav", 1), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1024 * 1024 + 1)]
    public async Task ReadText_rejects_a_limit_outside_the_allowed_range(int maxChars)
    {
        var storage = UnconnectedStorage();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            storage.ReadTextAsync("sources/a.txt", maxChars, CancellationToken.None));
    }

    private static AzureBlobStorage UnconnectedStorage() =>
        new(new BlobServiceClient("UseDevelopmentStorage=true"), Options.Create(new BlobStorageOptions()));
}
