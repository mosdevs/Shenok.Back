namespace Shenok.Back.ContentService.Application.Common.Interfaces;

public sealed record BlobUpload(string StorageKey, Stream Content, string ContentType, long SizeBytes);

/// <summary>
/// A private object opened for sequential reading. The caller owns <see cref="Content"/> and must dispose it.
/// </summary>
public sealed record OpenedBlob(Stream Content, string ContentType, long Length);

public interface IBlobStorage
{
    /// <summary>
    /// Stores a private object. Reads at most <see cref="BlobUpload.SizeBytes"/> and fails when the stream is shorter.
    /// The key is chosen by the application and must be stored unchanged.
    /// </summary>
    Task PutAsync(BlobUpload upload, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a UTF-8 object. Fails when the object is larger than <paramref name="maxChars"/>.
    /// </summary>
    Task<string> ReadTextAsync(string storageKey, int maxChars, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a private object. Returns null when the object does not exist, never an empty stream in its place.
    /// </summary>
    Task<OpenedBlob?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}
