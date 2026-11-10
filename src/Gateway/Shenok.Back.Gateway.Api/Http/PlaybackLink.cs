using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Api.Playback;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal sealed class PlaybackLink(GatewayPlaybackSettings settings) : IPlaybackLink
{
    public string PlaylistUrl(Guid voiceId, Guid waveformId, long expiresUnixSeconds)
    {
        var resource = PlaybackPaths.ScanPlaylist(settings.PathPrefix, voiceId, waveformId);
        var signature = PlaybackSignature.Sign(settings.SigningKey, resource, expiresUnixSeconds);
        // waveformId is already in the resource query, so exp and sig are appended.
        return $"{settings.Authority}{resource}&exp={expiresUnixSeconds}&sig={signature}";
    }

    public string PlaylistUrl(Guid voiceId, long expiresUnixSeconds)
    {
        var resource = PlaybackPaths.Playlist(settings.PathPrefix, voiceId);
        var signature = PlaybackSignature.Sign(settings.SigningKey, resource, expiresUnixSeconds);
        return $"{settings.Authority}{resource}?exp={expiresUnixSeconds}&sig={signature}";
    }

    public bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature)
    {
        if (!Accepts(signature) || voiceId == Guid.Empty || waveformId == Guid.Empty)
            return false;

        return PlaybackSignature.Matches(
            settings.SigningKey,
            PlaybackPaths.ScanPlaylist(settings.PathPrefix, voiceId, waveformId),
            expiresUnixSeconds,
            signature);
    }

    public bool SignatureMatches(Guid voiceId, long expiresUnixSeconds, string signature)
    {
        if (!Accepts(signature) || voiceId == Guid.Empty)
            return false;

        return PlaybackSignature.Matches(
            settings.SigningKey,
            PlaybackPaths.Playlist(settings.PathPrefix, voiceId),
            expiresUnixSeconds,
            signature);
    }

    public bool SegmentSignatureMatches(Guid voiceId, string relativePath, long expiresUnixSeconds, string signature)
    {
        if (!Accepts(signature) || voiceId == Guid.Empty || string.IsNullOrWhiteSpace(relativePath))
            return false;

        return PlaybackSignature.Matches(
            settings.SigningKey,
            PlaybackPaths.Segment(settings.PathPrefix, voiceId, relativePath),
            expiresUnixSeconds,
            signature);
    }

    private static bool Accepts(string signature) =>
        !string.IsNullOrWhiteSpace(signature) && signature.Length <= 128;
}
