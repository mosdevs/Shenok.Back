using System.Net;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed record DownstreamCall(HttpMethod Method, string Target, string? Body);

public sealed class DownstreamRecorder
{
    public int Calls { get; private set; }

    public Uri? RequestUri { get; private set; }

    public IReadOnlyDictionary<string, string[]> Headers { get; private set; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    public List<DownstreamCall> History { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage?>? Handle { get; set; }

    public async Task RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        RequestUri = request.RequestUri;
        Headers = request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        History.Add(new DownstreamCall(request.Method, request.RequestUri?.PathAndQuery ?? string.Empty, body));
    }
}

internal sealed class RecordingHandler(DownstreamRecorder recorder) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await recorder.RecordAsync(request, cancellationToken);
        return recorder.Handle?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NoContent);
    }
}
