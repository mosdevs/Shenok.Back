using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Api.Playback;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class PlaybackLinkTests
{
    private static readonly string Key = new('k', GatewayPlaybackSettings.MinSigningKeyLength);

    [Fact]
    public void Scan_url_keeps_the_waveform_and_round_trips()
    {
        var settings = new GatewayPlaybackSettings("https://api.example.com/edge", Key);
        var links = new PlaybackLink(settings);
        var voiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var waveformId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        const long expires = 1_700_000_200;

        var url = links.PlaylistUrl(voiceId, waveformId, expires);

        Assert.StartsWith(
            "https://api.example.com/edge/audio/11111111-1111-1111-1111-111111111111/playlist.m3u8?waveformId=22222222-2222-2222-2222-222222222222&exp=1700000200&sig=",
            url,
            StringComparison.Ordinal);
        var signature = url[(url.LastIndexOf("sig=", StringComparison.Ordinal) + 4)..];
        Assert.True(links.SignatureMatches(voiceId, waveformId, expires, signature));
        Assert.False(links.SignatureMatches(voiceId, expires, signature));
    }

    [Fact]
    public void Direct_playlist_and_segment_round_trip_without_a_waveform()
    {
        var settings = new GatewayPlaybackSettings("http://127.0.0.1:5080", Key);
        var links = new PlaybackLink(settings);
        var voiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const long expires = 1_700_000_200;

        var url = links.PlaylistUrl(voiceId, expires);

        Assert.StartsWith(
            "http://127.0.0.1:5080/audio/11111111-1111-1111-1111-111111111111/playlist.m3u8?exp=1700000200&sig=",
            url,
            StringComparison.Ordinal);
        Assert.DoesNotContain("waveformId", url, StringComparison.Ordinal);
        var signature = url[(url.LastIndexOf("sig=", StringComparison.Ordinal) + 4)..];
        Assert.True(links.SignatureMatches(voiceId, expires, signature));

        var resource = PlaybackPaths.Segment(string.Empty, voiceId, "seg_00000.ts");
        var segmentSignature = PlaybackSignature.Sign(Key, resource, expires);
        Assert.Equal("/audio/11111111-1111-1111-1111-111111111111/segments/seg_00000.ts", resource);
        Assert.True(links.SegmentSignatureMatches(voiceId, "seg_00000.ts", expires, segmentSignature));
        Assert.False(links.SegmentSignatureMatches(voiceId, "other.ts", expires, segmentSignature));
    }
}
