using System.Net;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class DownstreamRecorder
{
    public int Calls { get; private set; }

    public Uri? RequestUri { get; private set; }

    public IReadOnlyDictionary<string, string[]> Headers { get; private set; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    public void Record(HttpRequestMessage request)
    {
        Calls++;
        RequestUri = request.RequestUri;
        Headers = request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed class RecordingHandler(DownstreamRecorder recorder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        recorder.Record(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }
}
