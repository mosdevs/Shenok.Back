namespace Shenok.Back.ContentService.Application.Common.Interfaces;

public sealed record SignedPlaylist(string Body, long ExpiresUnixSeconds);

public interface IPlaylistSigner
{
    /// <summary>
    /// Rewrites a relative HLS playlist so every segment URI is a short-lived gateway URL.
    /// </summary>
    SignedPlaylist Sign(Guid voiceId, string playlistBody, string segmentPrefix);
}
