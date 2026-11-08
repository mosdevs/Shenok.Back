using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Pages.UseCases.GetPage;
using Shenok.Back.ContentService.Application.Voices.UseCases.GetVoice;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalCatalogController(GetPageUseCase getPage, GetVoiceUseCase getVoice) : ControllerBase
{
    [HttpGet("/internal/pages/{pageId:guid}")]
    public Task<IActionResult> GetPage(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new GetPageRequest(pageId),
            (request, token) => getPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpGet("/internal/voices/{voiceId:guid}")]
    public Task<IActionResult> GetVoice(Guid voiceId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new GetVoiceRequest(voiceId),
            (request, token) => getVoice.ExecuteAsync(request, token),
            cancellationToken);
}
