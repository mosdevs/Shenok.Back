namespace Shenok.Back.Gateway.Api.Playback;

/// <summary>
/// Resource strings Content signs into playlist and segment URLs.
/// The segment escape matches <c>HlsPlaylistSigner</c>: each path piece is <see cref="Uri.EscapeDataString"/>.
/// The Gateway does not reference Content, so the format is repeated here.
/// </summary>
internal static class PlaybackPaths
{
    public static string Playlist(string pathPrefix, Guid voiceId) =>
        $"{pathPrefix}/audio/{voiceId:D}/playlist.m3u8";

    public static string ScanPlaylist(string pathPrefix, Guid voiceId, Guid waveformId) =>
        $"{Playlist(pathPrefix, voiceId)}?waveformId={waveformId:D}";

    public static string Segment(string pathPrefix, Guid voiceId, string relativePath) =>
        $"{pathPrefix}/audio/{voiceId:D}/segments/{Escape(relativePath)}";

    public static string ContentSegment(Guid voiceId, string relativePath) =>
        $"internal/voices/{voiceId:D}/segments/{Escape(relativePath)}";

    public static string Escape(string relativePath) =>
        string.Join('/', relativePath.Split('/').Select(static part => Uri.EscapeDataString(part)));
}
