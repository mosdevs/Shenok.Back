using Microsoft.AspNetCore.Mvc;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Voices.UseCases.GetVoice;
using Shenok.Back.ContentService.Application.Voices.UseCases.UpdateVoiceTitle;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
[Route("voices")]
public sealed class VoicesController(GetVoiceUseCase getVoice, UpdateVoiceTitleUseCase updateTitle) : ControllerBase
{
    [HttpGet("{voiceId:guid}")]
    public Task<IActionResult> Get(Guid voiceId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new GetVoiceRequest(voiceId),
            (request, token) => getVoice.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPut("{voiceId:guid}")]
    public Task<IActionResult> UpdateTitle(Guid voiceId, UpdateVoiceTitleBody body, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new UpdateVoiceTitleRequest(voiceId, body.Title ?? string.Empty),
            (request, token) => updateTitle.ExecuteAsync(request, token),
            cancellationToken);
}

public sealed record UpdateVoiceTitleBody(string? Title);
