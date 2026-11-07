using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Infrastructure.Packaging;
using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Packaging;

public sealed class FfmpegAudioPackagerGuardTests
{
    [Fact]
    public async Task Invalid_source_key_fails_before_a_process_or_a_network_call()
    {
        var result = await Packager().PackageAsync("../secret", 60, CancellationToken.None);

        var failed = Assert.IsType<AudioPackageResult.Failed>(result);
        Assert.Equal("source-invalid", failed.Reason);
    }

    [Theory]
    [MemberData(nameof(InvalidDurations))]
    public async Task Non_positive_duration_limit_is_rejected(double maxDuration)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Packager().PackageAsync("sources/file.bin", maxDuration, CancellationToken.None));
    }

    public static IEnumerable<object[]> InvalidDurations()
    {
        yield return [0d];
        yield return [-1d];
        yield return [double.NaN];
        yield return [double.PositiveInfinity];
        yield return [double.NegativeInfinity];
    }

    private static FfmpegAudioPackager Packager() =>
        new(
            new AzureBlobStorage(new BlobServiceClient("UseDevelopmentStorage=true"), Options.Create(new BlobStorageOptions())),
            Options.Create(new PackagingOptions()),
            NullLogger<FfmpegAudioPackager>.Instance);
}

[Collection(InfrastructureFixture.Name)]
public sealed class FfmpegAudioPackagerTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Missing_source_fails_without_writing_a_package()
    {
        var (storage, container) = IsolatedStorage();
        var result = await fixture.CreatePackager(storage).PackageAsync($"sources/{Guid.NewGuid():N}", 60, CancellationToken.None);

        var failed = Assert.IsType<AudioPackageResult.Failed>(result);
        Assert.Equal("source-missing", failed.Reason);
        Assert.Empty(await ListAsync(container));
    }

    [Fact]
    public async Task Source_longer_than_the_limit_is_not_packaged()
    {
        var (storage, container) = IsolatedStorage();
        var wav = SilentWav(sampleRate: 8000, seconds: 1);
        var key = $"sources/{Guid.NewGuid():N}.wav";
        await UploadAsync(storage, key, wav);
        var root = TempRootCount();

        var result = await fixture.CreatePackager(storage).PackageAsync(key, maxDurationSeconds: 0.05, CancellationToken.None);

        var failed = Assert.IsType<AudioPackageResult.Failed>(result);
        Assert.Equal("duration-exceeded", failed.Reason);
        Assert.Equal([key], await ListAsync(container));
        Assert.Equal(root, TempRootCount());
    }

    [Fact]
    public async Task Package_stores_a_relative_playlist_and_its_segments()
    {
        var (storage, container) = IsolatedStorage();
        var wav = SilentWav(sampleRate: 8000, seconds: 1);
        var key = $"sources/{Guid.NewGuid():N}.wav";
        await UploadAsync(storage, key, wav);
        var root = TempRootCount();

        var result = await fixture.CreatePackager(storage).PackageAsync(key, maxDurationSeconds: 60, CancellationToken.None);

        var succeeded = Assert.IsType<AudioPackageResult.Succeeded>(result);
        Assert.InRange(succeeded.DurationSeconds, 0.5, 2);
        Assert.StartsWith("packages/", succeeded.PlaylistStorageKey, StringComparison.Ordinal);
        Assert.EndsWith("index.m3u8", succeeded.PlaylistStorageKey, StringComparison.Ordinal);
        Assert.EndsWith("/", succeeded.SegmentPrefix, StringComparison.Ordinal);
        Assert.StartsWith(succeeded.SegmentPrefix, succeeded.PlaylistStorageKey, StringComparison.Ordinal);

        var playlist = await storage.ReadTextAsync(succeeded.PlaylistStorageKey, 16 * 1024, CancellationToken.None);
        Assert.StartsWith("#EXTM3U", playlist, StringComparison.Ordinal);
        Assert.Contains("seg_", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("://", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", playlist, StringComparison.Ordinal);

        var names = await ListAsync(container);
        Assert.Contains(succeeded.PlaylistStorageKey, names);
        Assert.Contains(names, name => name.StartsWith(succeeded.SegmentPrefix, StringComparison.Ordinal) && name.EndsWith(".ts", StringComparison.Ordinal));
        Assert.Equal(root, TempRootCount());
    }

    private (AzureBlobStorage Storage, string Container) IsolatedStorage()
    {
        var container = "pkg" + Guid.NewGuid().ToString("N");
        return (fixture.CreateBlobStorage(container), container);
    }

    private static async Task UploadAsync(AzureBlobStorage storage, string key, byte[] body)
    {
        await using var stream = new MemoryStream(body);
        await storage.PutAsync(new BlobUpload(key, stream, "audio/wav", body.Length), CancellationToken.None);
    }

    private async Task<IReadOnlyList<string>> ListAsync(string container)
    {
        var names = new List<string>();
        await foreach (var blob in fixture.CreateBlobClient().GetBlobContainerClient(container).GetBlobsAsync())
            names.Add(blob.Name);
        return names;
    }

    private static int TempRootCount()
    {
        var root = Path.Combine(Path.GetTempPath(), "shenok-content");
        return Directory.Exists(root) ? Directory.GetDirectories(root).Length : 0;
    }

    private static byte[] SilentWav(int sampleRate, int seconds)
    {
        var dataBytes = sampleRate * seconds * 2;
        var buffer = new byte[44 + dataBytes];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        BitConverter.GetBytes(36 + dataBytes).CopyTo(buffer, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(buffer, 12);
        BitConverter.GetBytes(16).CopyTo(buffer, 16);
        BitConverter.GetBytes((short)1).CopyTo(buffer, 20);
        BitConverter.GetBytes((short)1).CopyTo(buffer, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(buffer, 24);
        BitConverter.GetBytes(sampleRate * 2).CopyTo(buffer, 28);
        BitConverter.GetBytes((short)2).CopyTo(buffer, 32);
        BitConverter.GetBytes((short)16).CopyTo(buffer, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(buffer, 36);
        BitConverter.GetBytes(dataBytes).CopyTo(buffer, 40);
        return buffer;
    }
}
