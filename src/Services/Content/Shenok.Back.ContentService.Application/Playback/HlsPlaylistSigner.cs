using System.Text;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Application.Playback;

public sealed class HlsPlaylistSigner(PlaybackSigningSettings settings, TimeProvider clock) : IPlaylistSigner
{
    public const int MaxPlaylistChars = 256 * 1024;
    public const int MaxLineChars = 4096;
    public const int MaxSegments = 8_000;

    public SignedPlaylist Sign(Guid voiceId, string playlistBody, string segmentPrefix)
    {
        if (voiceId == Guid.Empty)
            throw new ArgumentException("Voice id is required.", nameof(voiceId));
        if (string.IsNullOrWhiteSpace(playlistBody) || playlistBody.Length > MaxPlaylistChars)
            throw new ConflictException("The packaged playlist cannot be issued.");
        if (string.IsNullOrWhiteSpace(segmentPrefix))
            throw new ConflictException("The packaged playlist cannot be issued.");

        var prefix = segmentPrefix.Trim();
        var expires = clock.GetUtcNow().Add(settings.Lifetime).ToUnixTimeSeconds();
        var body = Rewrite(voiceId, playlistBody, prefix, expires);
        return new SignedPlaylist(body, expires);
    }

    private string Rewrite(Guid voiceId, string playlistBody, string segmentPrefix, long expires)
    {
        var normalized = playlistBody
            .TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        if (!normalized.StartsWith("#EXTM3U", StringComparison.Ordinal))
            throw new ConflictException("The packaged playlist cannot be issued.");

        var lines = normalized.Split('\n');
        var limit = lines.Length;
        if (limit > 1 && lines[^1].Length == 0)
            limit--;

        var written = new StringBuilder(normalized.Length + limit * 96);
        var mediaCount = 0;
        for (var i = 0; i < limit; i++)
        {
            var line = lines[i];
            if (line.Length > MaxLineChars || line.Contains('\0'))
                throw new ConflictException("The packaged playlist cannot be issued.");

            if (line.Length == 0)
            {
                written.Append('\n');
                continue;
            }

            if (line[0] == '#')
            {
                written.Append(line.Contains("URI", StringComparison.OrdinalIgnoreCase)
                    ? RewriteTag(line, voiceId, segmentPrefix, expires)
                    : CopyTag(line, segmentPrefix));
            }
            else
            {
                if (++mediaCount > MaxSegments)
                    throw new ConflictException("The packaged playlist cannot be issued.");

                written.Append(SignUrl(voiceId, RequirePublicPath(NormalizeSegment(line, segmentPrefix), segmentPrefix), expires));
            }

            written.Append('\n');
        }

        if (mediaCount == 0)
            throw new ConflictException("The packaged playlist cannot be issued.");

        return written.ToString();
    }

    private static string CopyTag(string line, string segmentPrefix)
    {
        if (line.Contains('\\')
            || line.Contains("://", StringComparison.Ordinal)
            || (segmentPrefix.Length >= 8 && line.Contains(segmentPrefix, StringComparison.Ordinal)))
        {
            throw new ConflictException("The packaged playlist cannot be issued.");
        }

        return line;
    }

    private string RewriteTag(string line, Guid voiceId, string segmentPrefix, long expires)
    {
        if (line.Contains('\\') || line.Contains("://", StringComparison.Ordinal))
            throw new ConflictException("The packaged playlist cannot be issued.");

        var rewritten = new StringBuilder(line.Length + 128);
        var index = 0;
        var replacements = 0;
        while (index < line.Length)
        {
            var marker = line.IndexOf("URI=\"", index, StringComparison.Ordinal);
            if (marker < 0)
            {
                rewritten.Append(line.AsSpan(index));
                break;
            }

            var valueStart = marker + 5;
            var valueEnd = line.IndexOf('"', valueStart);
            if (valueEnd < 0)
                throw new ConflictException("The packaged playlist cannot be issued.");

            rewritten.Append(line.AsSpan(index, marker - index));
            rewritten.Append("URI=\"");
            rewritten.Append(SignUrl(
                voiceId,
                RequirePublicPath(NormalizeSegment(line[valueStart..valueEnd], segmentPrefix), segmentPrefix),
                expires));
            rewritten.Append('"');
            replacements++;
            index = valueEnd + 1;
        }

        if (replacements == 0)
            throw new ConflictException("The packaged playlist cannot be issued.");

        return rewritten.ToString();
    }

    private static string RequirePublicPath(string relativePath, string segmentPrefix)
    {
        if (segmentPrefix.Length >= 8 && relativePath.Contains(segmentPrefix, StringComparison.Ordinal))
            throw new ConflictException("The packaged playlist cannot be issued.");

        return relativePath;
    }

    private string SignUrl(Guid voiceId, string relativePath, long expires)
    {
        var escaped = string.Join('/', relativePath.Split('/').Select(static part => Uri.EscapeDataString(part)));
        var resource = $"{settings.PathPrefix}/audio/{voiceId:D}/segments/{escaped}";
        var signature = PlaybackSignature.Sign(settings.SigningKey, resource, expires);
        return $"{settings.Authority}{resource}?exp={expires}&sig={signature}";
    }

    private static string NormalizeSegment(string raw, string segmentPrefix) =>
        SegmentPath.TryNormalize(raw, segmentPrefix, out var relative)
            ? relative
            : throw new ConflictException("The packaged playlist cannot be issued.");
}
