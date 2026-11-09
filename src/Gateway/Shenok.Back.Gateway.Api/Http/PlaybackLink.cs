using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Api.Playback;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal sealed class PlaybackLink(GatewayPlaybackSettings settings) : IPlaybackLink
{
    public string PlaylistUrl(Guid voiceId, Guid waveformId, long expiresUnixSeconds)
    {
        var resource = Resource(voiceId, waveformId);
        var signature = PlaybackSignature.Sign(settings.SigningKey, resource, expiresUnixSeconds);
        // waveformId is already in the resource query, so exp and sig are appended.
        return $"{settings.Authority}{resource}&exp={expiresUnixSeconds}&sig={signature}";
    }

    public bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || signature.Length > 128)
            return false;

        return PlaybackSignature.Matches(
            settings.SigningKey,
            Resource(voiceId, waveformId),
            expiresUnixSeconds,
            signature);
    }

    private string Resource(Guid voiceId, Guid waveformId) =>
        $"{settings.PathPrefix}/audio/{voiceId:D}/playlist.m3u8?waveformId={waveformId:D}";
}
