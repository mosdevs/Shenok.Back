namespace Shenok.Back.ContentService.Application.Common;

internal static class StorageKeys
{
    public static string NewSourceKey() => $"sources/{Guid.CreateVersion7():N}";
}
