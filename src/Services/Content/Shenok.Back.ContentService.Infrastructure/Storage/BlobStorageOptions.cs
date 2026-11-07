namespace Shenok.Back.ContentService.Infrastructure.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ContainerName { get; set; } = "content";

    public static bool IsValid(BlobStorageOptions options) => IsContainerName(options.ContainerName);

    private static bool IsContainerName(string name)
    {
        if (name.Length is < 3 or > 63 || name[0] == '-' || name[^1] == '-' || name.Contains("--", StringComparison.Ordinal))
            return false;

        foreach (var character in name)
        {
            if (char.IsAsciiDigit(character) || character == '-' || character is >= 'a' and <= 'z')
                continue;

            return false;
        }

        return true;
    }
}
