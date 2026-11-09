using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shenok.Back.Application.Abstractions.Exceptions;

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
}
