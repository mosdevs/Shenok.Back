using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Infrastructure.Storage;

public sealed class AzureBlobStorage : IBlobStorage
{
    private const int MaxTextChars = 1024 * 1024;
    private readonly BlobContainerClient _container;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public AzureBlobStorage(BlobServiceClient client, IOptions<BlobStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _container = client.GetBlobContainerClient(options.Value.ContainerName);
    }

    internal async Task DownloadToFileAsync(string storageKey, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        StorageKeyRules.RequireObjectKey(storageKey);
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(storageKey);
        Response<BlobProperties> properties;
        try
        {
            properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new FileNotFoundException("Source audio is missing.");
        }

        if (properties.Value.ContentLength <= 0 || properties.Value.ContentLength > maxBytes)
            throw new InvalidDataException("Source audio is outside the allowed size.");

        await using var source = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > maxBytes)
                throw new InvalidDataException("Source audio is outside the allowed size.");

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total != properties.Value.ContentLength)
            throw new InvalidDataException("Source audio changed while it was being read.");
    }

    public async Task PutAsync(BlobUpload upload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        StorageKeyRules.RequireObjectKey(upload.StorageKey);
        var contentType = RequireContentType(upload.ContentType);
        if (upload.SizeBytes <= 0 || upload.SizeBytes > AudioAsset.MaxSourceSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(upload), "Declared size is outside the allowed range.");
        if (upload.Content is null)
            throw new ArgumentException("Upload content is required.", nameof(upload));

        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(upload.StorageKey);
        var bounded = new BoundedReadStream(upload.Content, upload.SizeBytes);
        var created = false;
        try
        {
            await blob.UploadAsync(bounded, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = "private, no-store"
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken);
            created = true;
            if (bounded.BytesRead != upload.SizeBytes)
                throw new InvalidDataException("Uploaded stream was shorter than the declared size.");
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            throw new InvalidOperationException("Storage key is already in use.", exception);
        }
        catch (Exception failure) when (created)
        {
            try
            {
                await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: CancellationToken.None);
            }
            catch (Exception deleteFailure)
            {
                throw new AggregateException(
                    "The object was not kept and could not be deleted.",
                    failure,
                    deleteFailure);
            }

            throw;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        StorageKeyRules.RequireObjectKey(storageKey);
        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(storageKey);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public async Task<string> ReadTextAsync(string storageKey, int maxChars, CancellationToken cancellationToken)
    {
        StorageKeyRules.RequireObjectKey(storageKey);
        if (maxChars is <= 0 or > MaxTextChars)
            throw new ArgumentOutOfRangeException(nameof(maxChars));

        await EnsureContainerAsync(cancellationToken);
        var blob = _container.GetBlobClient(storageKey);
        Response<BlobProperties> properties;
        try
        {
            properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new InvalidDataException("Stored text was not found.", exception);
        }

        if (properties.Value.ContentLength < 0 || properties.Value.ContentLength > maxChars)
            throw new InvalidDataException("Stored text is larger than the allowed size.");

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        await using var stream = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        var bounded = new BoundedReadStream(stream, properties.Value.ContentLength);
        using var reader = new StreamReader(bounded, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (bounded.BytesRead != properties.Value.ContentLength || text.Length > maxChars || text.Contains('\0'))
            throw new InvalidDataException("Stored text is larger than the allowed size.");

        return text;
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_ready)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ready)
                return;

            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            await _container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string RequireContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));

        var value = contentType.Trim();
        var separator = value.IndexOf(';');
        if (separator >= 0)
            value = value[..separator].Trim();

        if (value.Length is 0 or > AudioAsset.MaxContentTypeLength || !value.Contains('/'))
            throw new ArgumentException("Content type is invalid.", nameof(contentType));

        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '/' or '.' or '+' or '-')
                continue;

            throw new ArgumentException("Content type is invalid.", nameof(contentType));
        }

        return value.ToLowerInvariant();
    }
}
