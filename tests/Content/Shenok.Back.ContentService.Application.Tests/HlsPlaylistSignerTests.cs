using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Playback;

namespace Shenok.Back.ContentService.Application.Tests;

public class HlsPlaylistSignerTests
{
    private const string Key = "dev-only-signing-key-32-characters";
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sign_rewrites_segments_and_map_uris_with_a_verifiable_signature()
    {
        var signer = CreateSigner();
        var voiceId = Guid.NewGuid();
        var playlist = """
            #EXTM3U
            #EXT-X-TARGETDURATION:10
            #EXTINF:10.0,
            hls/packagesegment/seg000.aac
            #EXT-X-MAP:URI="hls/packagesegment/init.mp4"
            """;

        var signed = signer.Sign(voiceId, playlist, "hls/packagesegment/");

        Assert.DoesNotContain("hls/packagesegment", signed.Body);
        Assert.DoesNotContain("http://", signed.Body, StringComparison.Ordinal);
        Assert.Equal(Now.AddMinutes(10).ToUnixTimeSeconds(), signed.ExpiresUnixSeconds);

        var urls = signed.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => line.Split('"'))
            .Where(part => part.StartsWith("https://gateway.example/", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, urls.Length);
        foreach (var url in urls)
            AssertSignature(url, voiceId);
    }

    [Theory]
    [InlineData("#EXTM3U\n#EXTINF:1,\nhttps://blob.example/seg.ts\n")]
    [InlineData("#EXTM3U\n#EXTINF:1,\n../secret.ts\n")]
    [InlineData("#EXTM3U\n#EXTINF:1,\n/secret.ts\n")]
    [InlineData("#EXTM3U\n#EXT-X-KEY:URI=secret.key\nseg.ts\n")]
    [InlineData("not a playlist\nseg.ts\n")]
    public void Sign_rejects_a_playlist_that_could_leak_or_escape(string playlist)
    {
        var signer = CreateSigner();
        Assert.Throws<ConflictException>(() => signer.Sign(Guid.NewGuid(), playlist, "hls/packagesegment/"));
    }

    [Fact]
    public void Settings_reject_a_non_loopback_http_gateway_and_a_short_key()
    {
        Assert.Throws<ArgumentException>(() => new PlaybackSigningSettings("http://cdn.example", Key));
        Assert.Throws<ArgumentException>(() => new PlaybackSigningSettings("https://gateway.example", "short"));
        var loopback = new PlaybackSigningSettings("http://localhost:5000", Key);
        Assert.Equal("http://localhost:5000", loopback.Authority);
    }

    private static HlsPlaylistSigner CreateSigner() =>
        new(new PlaybackSigningSettings("https://gateway.example", Key), new FixedClock(Now));

    private static void AssertSignature(string url, Guid voiceId)
    {
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?').Split('&');
        var expires = long.Parse(query.Single(part => part.StartsWith("exp=", StringComparison.Ordinal))["exp=".Length..]);
        var signature = query.Single(part => part.StartsWith("sig=", StringComparison.Ordinal))["sig=".Length..];
        Assert.StartsWith($"/audio/{voiceId:D}/segments/", uri.AbsolutePath);
        Assert.True(PlaybackSignature.Matches(Key, uri.AbsolutePath, expires, signature));
    }
}
