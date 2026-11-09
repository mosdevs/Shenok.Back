using System.Net.Http.Headers;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

/// <summary>
/// Posts the image to Recognition <c>POST internal/decode</c>.
/// HTTP 404 is a miss. A hit is <c>waveformId</c>, <c>targetType</c>, and <c>targetId</c>.
/// </summary>
internal sealed class HttpScanDecoder(IHttpClientFactory factory) : IScanDecoder
{
    public async Task<WaveformHit?> DecodeAsync(ScanImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image.Image);

        var client = factory.CreateClient(GatewayClients.Recognition);
        using var body = new ByteArrayContent(image.Image);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType);

        var payload = await Downstream.PostMissableAsync<DecodePayload>(
            client,
            "internal/decode",
            body,
            "Recognition",
            cancellationToken);
        if (payload is null
            || payload.WaveformId == Guid.Empty
            || payload.TargetId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.TargetType))
        {
            return null;
        }

        if (!WaveformTargets.IsPage(payload.TargetType) && !WaveformTargets.IsVoice(payload.TargetType))
            return null;

        var targetType = WaveformTargets.IsPage(payload.TargetType) ? WaveformTargets.Page : WaveformTargets.Voice;
        return new WaveformHit(payload.WaveformId, targetType, payload.TargetId);
    }

    private sealed record DecodePayload(Guid WaveformId, string? TargetType, Guid TargetId);
}
