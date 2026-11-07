using Microsoft.AspNetCore.Mvc;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RegisterWaveform;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RevokeWaveform;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
[Route("waveforms")]
public sealed class WaveformsController(
    RegisterWaveformUseCase registerWaveform,
    RevokeWaveformUseCase revokeWaveform) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Register(RegisterWaveformRequest request, CancellationToken cancellationToken) =>
        UseCaseHttp.CreatedAsync(
            HttpContext,
            request,
            (body, token) => registerWaveform.ExecuteAsync(body, token),
            created => $"/waveforms/{created.WaveformId}",
            cancellationToken);

    [HttpPost("{waveformId:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid waveformId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new RevokeWaveformRequest(waveformId),
            (request, token) => revokeWaveform.ExecuteAsync(request, token),
            cancellationToken);
}
