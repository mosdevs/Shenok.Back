using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

/// <summary>
/// Used when Recognition is not configured and the fixture decoder is off.
/// Every image is a miss, so a scan cannot unlock a page.
/// </summary>
internal sealed class ClosedScanDecoder : IScanDecoder
{
    public Task<WaveformHit?> DecodeAsync(ScanImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<WaveformHit?>(null);
    }
}
