using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class WaveformsController(IContentGateway content) : ControllerBase
{
    [HttpPost("/waveforms")]
    public async Task<IActionResult> Register(WaveformBody? body, CancellationToken cancellationToken)
    {
        var waveform = body ?? new WaveformBody(null, Guid.Empty);
        var reply = await content.RegisterWaveformAsync(waveform.TargetType, waveform.TargetId, cancellationToken);
        return ProxiedResults.From(reply);
    }

    [HttpPost("/waveforms/{waveformId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid waveformId, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.RevokeWaveformAsync(waveformId, cancellationToken));
}

public sealed record WaveformBody(string? TargetType, Guid TargetId);
