using System.Text;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

/// <summary>
/// Development stand-in for Recognition. A real image is a miss.
/// A text payload is accepted only while <c>Gateway:AllowFixtureDecoder</c> is true:
/// <c>fixture:miss</c>, <c>fixture:page:{waveformId}:{pageId}</c>, or <c>fixture:voice:{waveformId}:{voiceId}</c>.
/// The public content type stays image/jpeg or image/png.
/// </summary>
internal sealed class FixtureScanDecoder : IScanDecoder
{
    public const string Prefix = "fixture:";

    private const int MaxFixtureBytes = 256;

    public Task<WaveformHit?> DecodeAsync(ScanImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(image.Image);

        if (image.Image.Length is 0 or > MaxFixtureBytes || image.Image[0] != (byte)'f')
            return Task.FromResult<WaveformHit?>(null);

        var text = Encoding.UTF8.GetString(image.Image);
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
            return Task.FromResult<WaveformHit?>(null);

        return Task.FromResult(Parse(text[Prefix.Length..].Trim()));
    }

    private static WaveformHit? Parse(string body)
    {
        if (body.Equals("miss", StringComparison.Ordinal))
            return null;

        var parts = body.Split(':');
        if (parts.Length != 3 || (!WaveformTargets.IsPage(parts[0]) && !WaveformTargets.IsVoice(parts[0])))
            return null;

        if (!Guid.TryParse(parts[1], out var waveformId) || waveformId == Guid.Empty
            || !Guid.TryParse(parts[2], out var targetId) || targetId == Guid.Empty)
        {
            return null;
        }

        var targetType = WaveformTargets.IsPage(parts[0]) ? WaveformTargets.Page : WaveformTargets.Voice;
        return new WaveformHit(waveformId, targetType, targetId);
    }
}
