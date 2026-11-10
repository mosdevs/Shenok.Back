using System.Diagnostics.CodeAnalysis;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Application.Playback;

/// <summary>
/// Public segment names use the same character rules as playlist signing.
/// A name may repeat this voice's storage prefix. Any other prefix is rejected
/// so the opened key cannot leave that prefix.
/// </summary>
internal static class SegmentPath
{
    public static bool TryNormalize(string? raw, string segmentPrefix, [NotNullWhen(true)] out string? relative)
    {
        relative = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        if (value.Length is 0 or > HlsPlaylistSigner.MaxLineChars)
            return false;

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || ch is '\\' or '?' or '#' or ':' or '%' or '\0')
                return false;
        }

        if (value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
            return false;

        var prefix = segmentPrefix.Trim('/');
        if (prefix.Length > 0 && value.StartsWith(prefix + "/", StringComparison.Ordinal))
            value = value[(prefix.Length + 1)..];

        var parts = value.Split('/');
        if (parts.Length is 0 or > 8)
            return false;

        foreach (var part in parts)
        {
            if (part.Length is 0 or > 128 || part is "." or "..")
                return false;

            foreach (var ch in part)
            {
                if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '_' or '-'))
                    return false;
            }
        }

        relative = string.Join('/', parts);
        return true;
    }

    public static bool TryStorageKey(string? rawPath, string segmentPrefix, [NotNullWhen(true)] out string? storageKey)
    {
        storageKey = null;
        if (string.IsNullOrWhiteSpace(segmentPrefix) || string.IsNullOrWhiteSpace(rawPath))
            return false;
        if (!TryNormalize(rawPath, segmentPrefix, out var relative))
            return false;

        var prefix = segmentPrefix.Trim();
        if (!prefix.EndsWith('/'))
            prefix += "/";

        var own = prefix.TrimEnd('/');
        var trimmed = rawPath.Trim();
        if (trimmed.Contains('/') && !trimmed.StartsWith(own + "/", StringComparison.Ordinal))
            return false;

        var key = prefix + relative;
        if (key.Length > AudioAsset.MaxKeyLength || !key.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        storageKey = key;
        return true;
    }
}
