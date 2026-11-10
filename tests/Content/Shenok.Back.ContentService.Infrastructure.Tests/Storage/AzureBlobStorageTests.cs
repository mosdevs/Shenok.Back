using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Storage;

[Collection(InfrastructureFixture.Name)]
public sealed class AzureBlobStorageTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Put_stores_only_the_declared_bytes_and_stops_reading()
    {
        var declared = Enumerable.Repeat((byte)1, 16).ToArray();
        var extra = Enumerable.Repeat((byte)9, 32).ToArray();
        await using var content = new MemoryStream([.. declared, .. extra]);
        var key = $"sources/{Guid.NewGuid():N}";
        var storage = fixture.CreateBlobStorage();

        await storage.PutAsync(new BlobUpload(key, content, "Audio/WAV; charset=utf-8", declared.Length), CancellationToken.None);

        var downloaded = await DownloadAsync(key);
        Assert.Equal(declared, downloaded);
        Assert.Equal(declared.Length, content.Position);
        var properties = await Blob(key).GetPropertiesAsync();
        Assert.Equal("audio/wav", properties.Value.ContentType);
        Assert.Equal("private, no-store", properties.Value.CacheControl);
    }

    [Fact]
    public async Task Short_upload_is_removed_and_an_existing_key_cannot_be_replaced()
    {
        var key = $"sources/{Guid.NewGuid():N}";
        var storage = fixture.CreateBlobStorage();
        var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        await using (var content = new MemoryStream(original))
            await storage.PutAsync(new BlobUpload(key, content, "audio/wav", original.Length), CancellationToken.None);

        await using var shortContent = new MemoryStream(original.AsSpan(0, 4).ToArray());
        var shortKey = $"sources/{Guid.NewGuid():N}";
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            storage.PutAsync(new BlobUpload(shortKey, shortContent, "audio/wav", original.Length), CancellationToken.None));
        Assert.False((await Blob(shortKey).ExistsAsync()).Value);

        await using var replacement = new MemoryStream(Enumerable.Repeat((byte)3, original.Length).ToArray());
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.PutAsync(new BlobUpload(key, replacement, "audio/wav", original.Length), CancellationToken.None));
        Assert.Equal("Storage key is already in use.", exception.Message);
        Assert.DoesNotContain("AccountKey", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(original, await DownloadAsync(key));
    }

    [Fact]
    public async Task Container_stays_private_and_anonymous_read_is_rejected()
    {
        var key = $"sources/{Guid.NewGuid():N}";
        var storage = fixture.CreateBlobStorage();
        await using var content = new MemoryStream([7, 8, 9]);
        await storage.PutAsync(new BlobUpload(key, content, "audio/wav", 3), CancellationToken.None);

        var container = fixture.CreateBlobClient().GetBlobContainerClient("content");
        var properties = await container.GetPropertiesAsync();
        Assert.True(properties.Value.PublicAccess is null or PublicAccessType.None);

        var anonymous = new BlobClient(Blob(key).Uri);
        var denied = await Assert.ThrowsAsync<RequestFailedException>(() => anonymous.DownloadContentAsync());
        Assert.True(denied.Status is 401 or 403);
    }

    [Fact]
    public async Task ReadText_enforces_the_character_limit_and_rejects_a_null_byte()
    {
        var storage = fixture.CreateBlobStorage();
        var playlistKey = $"packages/{Guid.NewGuid():N}/index.m3u8";
        var playlist = "#EXTM3U\nseg_00000.ts\n";
        await using var playlistStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(playlist));
        await storage.PutAsync(new BlobUpload(playlistKey, playlistStream, "application/vnd.apple.mpegurl", playlistStream.Length), CancellationToken.None);

        Assert.Equal(playlist, await storage.ReadTextAsync(playlistKey, playlist.Length, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ReadTextAsync(playlistKey, playlist.Length - 1, CancellationToken.None));

        var binaryKey = $"sources/{Guid.NewGuid():N}";
        var binary = "a\0b"u8.ToArray();
        await using var binaryStream = new MemoryStream(binary);
        await storage.PutAsync(new BlobUpload(binaryKey, binaryStream, "audio/wav", binary.Length), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ReadTextAsync(binaryKey, 10, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ReadTextAsync($"sources/{Guid.NewGuid():N}", 10, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_is_idempotent_and_a_cancelled_put_does_not_create_an_object()
    {
        var storage = fixture.CreateBlobStorage();
        var key = $"sources/{Guid.NewGuid():N}";
        await storage.DeleteAsync(key, CancellationToken.None);
        await using var content = new MemoryStream([1, 2, 3, 4]);
        await storage.PutAsync(new BlobUpload(key, content, "audio/wav", 4), CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);
        Assert.False((await Blob(key).ExistsAsync()).Value);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var ignored = new MemoryStream([1, 2, 3, 4]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.PutAsync(new BlobUpload($"sources/{Guid.NewGuid():N}", ignored, "audio/wav", 4), cancellation.Token));
    }

    [Fact]
    public async Task OpenRead_returns_the_object_bytes_and_anonymous_read_still_fails()
    {
        var payload = new byte[] { 0x47, 1, 2, 3, 4, 5 };
        var key = $"packages/{Guid.NewGuid():N}/seg_00000.ts";
        var storage = fixture.CreateBlobStorage();
        await using (var content = new MemoryStream(payload))
            await storage.PutAsync(new BlobUpload(key, content, "video/mp2t", payload.Length), CancellationToken.None);

        var opened = await storage.OpenReadAsync(key, CancellationToken.None);

        Assert.NotNull(opened);
        Assert.Equal("video/mp2t", opened.ContentType);
        Assert.Equal(payload.LongLength, opened.Length);
        await using (opened.Content)
        {
            using var buffer = new MemoryStream();
            await opened.Content.CopyToAsync(buffer);
            Assert.Equal(payload, buffer.ToArray());
        }

        var container = fixture.CreateBlobClient().GetBlobContainerClient("content");
        var properties = await container.GetPropertiesAsync();
        Assert.True(properties.Value.PublicAccess is null or PublicAccessType.None);
        var anonymous = new BlobClient(Blob(key).Uri);
        var denied = await Assert.ThrowsAsync<RequestFailedException>(() => anonymous.DownloadContentAsync());
        Assert.True(denied.Status is 401 or 403);
    }

    [Fact]
    public async Task OpenRead_of_a_missing_object_is_not_found()
    {
        var storage = fixture.CreateBlobStorage();
        var key = $"packages/{Guid.NewGuid():N}/missing.ts";

        var opened = await storage.OpenReadAsync(key, CancellationToken.None);

        Assert.Null(opened);
        Assert.False((await Blob(key).ExistsAsync()).Value);
    }

    private BlobClient Blob(string key) =>
        fixture.CreateBlobClient().GetBlobContainerClient("content").GetBlobClient(key);

    private async Task<byte[]> DownloadAsync(string key)
    {
        var download = await Blob(key).DownloadContentAsync();
        return download.Value.Content.ToArray();
    }
}
