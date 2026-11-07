using Microsoft.AspNetCore.Mvc;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Pages.UseCases.ArchivePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.GetPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.PublishPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.ReorderVoices;
using Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;
using Shenok.Back.ContentService.Application.Voices.UseCases.AddVoiceToPage;
using Shenok.Back.ContentService.Application.Voices.UseCases.UploadVoice;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
[Route("pages")]
public sealed class PagesController(
    CreatePageUseCase createPage,
    GetPageUseCase getPage,
    UpdatePageMetadataUseCase updatePage,
    PublishPageUseCase publishPage,
    ArchivePageUseCase archivePage,
    ReorderVoicesUseCase reorderVoices,
    UploadVoiceUseCase uploadVoice,
    AddVoiceToPageUseCase addVoice) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create(CreatePageRequest request, CancellationToken cancellationToken) =>
        UseCaseHttp.CreatedAsync(
            HttpContext,
            request,
            (body, token) => createPage.ExecuteAsync(body, token),
            created => $"/pages/{created.PageId}",
            cancellationToken);

    [HttpGet("{pageId:guid}")]
    public Task<IActionResult> Get(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new GetPageRequest(pageId),
            (body, token) => getPage.ExecuteAsync(body, token),
            cancellationToken);

    [HttpPut("{pageId:guid}")]
    public Task<IActionResult> Update(Guid pageId, UpdatePageBody body, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new UpdatePageMetadataRequest(pageId, body.Title ?? string.Empty, body.Description, body.CoverImageUrl),
            (request, token) => updatePage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("{pageId:guid}/publish")]
    public Task<IActionResult> Publish(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new PublishPageRequest(pageId),
            (request, token) => publishPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("{pageId:guid}/archive")]
    public Task<IActionResult> Archive(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new ArchivePageRequest(pageId),
            (request, token) => archivePage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPut("{pageId:guid}/voices/order")]
    public Task<IActionResult> Reorder(Guid pageId, ReorderVoicesBody body, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new ReorderVoicesRequest(pageId, body.VoiceIds ?? []),
            (request, token) => reorderVoices.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("{pageId:guid}/voices/{voiceId:guid}")]
    public Task<IActionResult> AddVoice(Guid pageId, Guid voiceId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new AddVoiceToPageRequest(pageId, voiceId),
            (request, token) => addVoice.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("{pageId:guid}/voices")]
    [Consumes("multipart/form-data")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(AudioAsset.MaxSourceSizeBytes + 65_536)]
    [RequestFormLimits(MultipartBodyLengthLimit = AudioAsset.MaxSourceSizeBytes + 65_536)]
    public async Task<IActionResult> Upload(Guid pageId, [FromForm] UploadVoiceForm form, CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null)
        {
            return await UseCaseHttp.CreatedAsync(
                HttpContext,
                new UploadVoiceRequest(pageId, form.Title ?? string.Empty, string.Empty, 0, Stream.Null),
                (request, token) => uploadVoice.ExecuteAsync(request, token),
                created => $"/voices/{created.VoiceId}",
                cancellationToken);
        }

        await using var content = file.OpenReadStream();
        return await UseCaseHttp.CreatedAsync(
            HttpContext,
            new UploadVoiceRequest(pageId, form.Title ?? string.Empty, file.ContentType, file.Length, content),
            (request, token) => uploadVoice.ExecuteAsync(request, token),
            created => $"/voices/{created.VoiceId}",
            cancellationToken);
    }
}

public sealed record UpdatePageBody(string? Title, string? Description, string? CoverImageUrl);

public sealed record ReorderVoicesBody(IReadOnlyList<Guid>? VoiceIds);

public sealed class UploadVoiceForm
{
    public string? Title { get; set; }

    public IFormFile? File { get; set; }
}
