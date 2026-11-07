using System.Collections.Frozen;

namespace Shenok.Back.ContentService.Application.Common;

public static class AudioContentTypes
{
    private static readonly FrozenSet<string> Allowed = FrozenSet.ToFrozenSet(
        [
            "audio/mpeg",
            "audio/mp3",
            "audio/wav",
            "audio/x-wav",
            "audio/wave",
            "audio/aac",
            "audio/mp4",
            "audio/x-m4a",
            "audio/m4a",
            "audio/ogg",
            "audio/webm",
            "audio/flac",
            "audio/x-flac"
        ],
        StringComparer.Ordinal);

    public static bool TryNormalize(string? contentType, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var value = contentType.Trim();
        var separator = value.IndexOf(';');
        if (separator >= 0)
            value = value[..separator].Trim();

        if (value.Length == 0 || value.Contains('\r') || value.Contains('\n') || value.Contains('\0'))
            return false;

        normalized = value.ToLowerInvariant();
        return Allowed.Contains(normalized);
    }
}
