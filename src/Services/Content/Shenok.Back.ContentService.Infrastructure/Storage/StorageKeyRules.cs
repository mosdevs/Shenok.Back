using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Infrastructure.Storage;

internal static class StorageKeyRules
{
    public static void RequireObjectKey(string storageKey)
    {
        if (!IsObjectKey(storageKey))
            throw new ArgumentException("Storage key is invalid.", nameof(storageKey));
    }

    public static bool IsObjectKey(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > AudioAsset.MaxKeyLength)
            return false;
        if (storageKey[0] is '/' or '.' || storageKey[^1] == '/' || storageKey.Contains("//", StringComparison.Ordinal))
            return false;
        if (storageKey.Contains('\\') || storageKey.Contains(':') || storageKey.Contains("..", StringComparison.Ordinal))
            return false;

        var hasSeparator = false;
        foreach (var character in storageKey)
        {
            if (character == '/')
            {
                hasSeparator = true;
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.'))
                return false;
        }

        return hasSeparator;
    }
}
