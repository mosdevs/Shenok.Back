using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal static class DownstreamJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Reads a downstream call into an application exception.
/// A client error keeps a short detail. A server error or a network failure becomes unavailable
/// and does not copy the downstream body.
/// </summary>
internal static class Downstream
{
    public static Task<T> GetAsync<T>(
        HttpClient client,
        string path,
        string service,
        CancellationToken cancellationToken)
        where T : class =>
        SendAsync(
            () => client.GetAsync(path, cancellationToken),
            service,
            ReadRequiredAsync<T>,
            cancellationToken);

    public static Task<T> PostAsync<T>(
        HttpClient client,
        string path,
        object body,
        string service,
        CancellationToken cancellationToken)
        where T : class =>
        SendAsync(
            () => client.PostAsJsonAsync(path, body, DownstreamJson.Options, cancellationToken),
            service,
            ReadRequiredAsync<T>,
            cancellationToken);

    public static Task PostAsync(
        HttpClient client,
        string path,
        object body,
        string service,
        CancellationToken cancellationToken) =>
        SendAsync(
            () => client.PostAsJsonAsync(path, body, DownstreamJson.Options, cancellationToken),
            service,
            static (_, _, _) => Task.FromResult(true),
            cancellationToken);

    public static async Task<T?> PostMissableAsync<T>(
        HttpClient client,
        string path,
        HttpContent body,
        string service,
        CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(path, body, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
                throw await MapFailureAsync(response, service, cancellationToken);

            return await ReadRequiredAsync<T>(response, service, cancellationToken);
        }
    }

    public static Task DeleteAsync(
        HttpClient client,
        string path,
        string service,
        CancellationToken cancellationToken) =>
        SendAsync(
            () => client.DeleteAsync(path, cancellationToken),
            service,
            static (_, _, _) => Task.FromResult(true),
            cancellationToken);

    /// <summary>
    /// Copies the downstream status and body. A transport failure is unavailable.
    /// A problem status is not rewritten, so the caller can return it unchanged.
    /// </summary>
    public static async Task<ProxiedResponse> ExchangeAsync(
        Func<Task<HttpResponseMessage>> send,
        string service,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);
        using var response = await SendRawAsync(send, service, cancellationToken);
        var body = response.Content is null
            ? []
            : await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new ProxiedResponse(
            (int)response.StatusCode,
            response.Content?.Headers.ContentType?.ToString(),
            body,
            RelativeLocation(response.Headers.Location));
    }

    /// <summary>
    /// Opens a successful body without buffering it. A failure is mapped and the response is disposed.
    /// </summary>
    public static async Task<OpenedSegment> GetSegmentAsync(
        HttpClient client,
        string path,
        string service,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
                throw await MapFailureAsync(response, service, cancellationToken);
        }

        var length = response.Content.Headers.ContentLength;
        if (length is null or < 0)
        {
            response.Dispose();
            throw new UnavailableException($"{service} is unavailable.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new OpenedSegment(new OwnedStream(stream, response), length.Value);
    }

    private static async Task<T> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        string service,
        Func<HttpResponseMessage, string, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException exception)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await MapFailureAsync(response, service, cancellationToken);

            return await read(response, service, cancellationToken);
        }
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        string service,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(DownstreamJson.Options, cancellationToken);
            if (payload is null)
                throw new UnavailableException($"{service} is unavailable.");

            return payload;
        }
        catch (JsonException exception)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
    }

    private static async Task<Exception> MapFailureAsync(
        HttpResponseMessage response,
        string service,
        CancellationToken cancellationToken)
    {
        var detail = await TryDetailAsync(response, cancellationToken);
        return (int)response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => new DownstreamRequestException(detail ?? "The request was rejected."),
            StatusCodes.Status401Unauthorized => new UnavailableException($"{service} is unavailable."),
            StatusCodes.Status403Forbidden => new ForbiddenException(detail ?? "The request was rejected."),
            StatusCodes.Status404NotFound => new NotFoundException(detail ?? "The resource was not found."),
            StatusCodes.Status409Conflict => new ConflictException(detail ?? "The request conflicts with saved state."),
            _ => new UnavailableException($"{service} is unavailable.")
        };
    }

    private static async Task<string?> TryDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
            return null;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("detail", out var detail)
                || detail.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = detail.GetString();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 200 || text.Contains('\n') || text.Contains('\r'))
                return null;

            return text;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<HttpResponseMessage> SendRawAsync(
        Func<Task<HttpResponseMessage>> send,
        string service,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException exception)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UnavailableException($"{service} is unavailable.", exception);
        }
    }

    private static string? RelativeLocation(Uri? location)
    {
        if (location is null)
            return null;

        var value = location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
        if (value.Length is 0 or > 512
            || value[0] != '/'
            || value.Contains('\r')
            || value.Contains('\n')
            || value.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        return value;
    }

    /// <summary>
    /// Disposes the downstream response when the segment stream is disposed.
    /// </summary>
    private sealed class OwnedStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
