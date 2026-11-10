using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common.Interfaces;
using Shenok.Back.Gateway.Application.Playback.UseCases.StartPlayback;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class VoicesController(StartPlaybackUseCase startPlayback, IContentGateway content) : ControllerBase
{
    [HttpPost("/voices/{voiceId:guid}/playback")]
    public Task<IActionResult> Playback(Guid voiceId, CancellationToken cancellationToken) =>
        GatewayHttp.OkAsync(
            HttpContext,
            new StartPlaybackRequest(GatewayCaller.RequireUserId(User), voiceId),
            (request, token) => startPlayback.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPut("/voices/{voiceId:guid}")]
    public async Task<IActionResult> UpdateTitle(Guid voiceId, VoiceTitleBody? body, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.UpdateVoiceTitleAsync(voiceId, body?.Title, cancellationToken));
}

public sealed record VoiceTitleBody(string? Title);
