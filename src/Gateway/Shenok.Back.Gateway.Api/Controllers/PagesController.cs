using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;
using Shenok.Back.Gateway.Application.Follows.UseCases.FollowPage;
using Shenok.Back.Gateway.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.Gateway.Application.Pages.UseCases.OpenPage;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class PagesController(
    OpenPageUseCase openPage,
    FollowPageUseCase followPage,
    UnfollowPageUseCase unfollowPage,
    IContentGateway content) : ControllerBase
{
    [HttpGet("/pages/{pageId:guid}")]
    public Task<IActionResult> Get(Guid pageId, CancellationToken cancellationToken) =>
        GatewayHttp.OkAsync(
            HttpContext,
            new OpenPageRequest(GatewayCaller.RequireUserId(User), pageId, GatewayCaller.IsAdmin(User)),
            (request, token) => openPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("/pages/{pageId:guid}/follow")]
    public Task<IActionResult> Follow(Guid pageId, CancellationToken cancellationToken) =>
        GatewayHttp.OkAsync(
            HttpContext,
            new FollowPageRequest(GatewayCaller.RequireUserId(User), pageId),
            (request, token) => followPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpDelete("/pages/{pageId:guid}/follow")]
    public Task<IActionResult> Unfollow(Guid pageId, CancellationToken cancellationToken) =>
        GatewayHttp.OkAsync(
            HttpContext,
            new UnfollowPageRequest(GatewayCaller.RequireUserId(User), pageId),
            (request, token) => unfollowPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("/pages")]
    public async Task<IActionResult> Create(PageWriteBody? body, CancellationToken cancellationToken)
    {
        var page = body ?? new PageWriteBody(null, null, null);
        var reply = await content.CreatePageAsync(page.Title, page.Description, page.CoverImageUrl, cancellationToken);
        return ProxiedResults.From(reply);
    }

    [HttpPut("/pages/{pageId:guid}")]
    public async Task<IActionResult> Update(Guid pageId, PageWriteBody? body, CancellationToken cancellationToken)
    {
        var page = body ?? new PageWriteBody(null, null, null);
        var reply = await content.UpdatePageAsync(pageId, page.Title, page.Description, page.CoverImageUrl, cancellationToken);
        return ProxiedResults.From(reply);
    }

    [HttpPost("/pages/{pageId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid pageId, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.PublishPageAsync(pageId, cancellationToken));

    [HttpPost("/pages/{pageId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid pageId, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.ArchivePageAsync(pageId, cancellationToken));

    [HttpPut("/pages/{pageId:guid}/voices/order")]
    public async Task<IActionResult> Reorder(Guid pageId, ReorderVoicesBody? body, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.ReorderVoicesAsync(pageId, body?.VoiceIds, cancellationToken));

    [HttpPost("/pages/{pageId:guid}/voices/{voiceId:guid}")]
    public async Task<IActionResult> AddVoice(Guid pageId, Guid voiceId, CancellationToken cancellationToken) =>
        ProxiedResults.From(await content.AddVoiceAsync(pageId, voiceId, cancellationToken));

    [HttpPost("/pages/{pageId:guid}/voices")]
    [Consumes("multipart/form-data")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(GatewayLimits.MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = GatewayLimits.MaxUploadRequestBytes)]
    [RejectOversizedUpload]
    public async Task<IActionResult> Upload(
        Guid pageId,
        [FromForm] string? title,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            var missing = await content.UploadVoiceAsync(pageId, title, null, 0, Stream.Null, cancellationToken);
            return ProxiedResults.From(missing);
        }

        await using var source = file.OpenReadStream();
        var reply = await content.UploadVoiceAsync(
            pageId,
            title,
            file.ContentType,
            file.Length,
            source,
            cancellationToken);
        return ProxiedResults.From(reply);
    }
}

/// <summary>
/// Returns 413 before the form value provider reads the body.
/// The shared exception middleware would otherwise turn Kestrel's size failure into a 400 validation problem.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RejectOversizedUploadAttribute : Attribute, IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > GatewayLimits.MaxUploadRequestBytes)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            return Task.CompletedTask;
        }

        return next();
    }
}

public sealed record PageWriteBody(string? Title, string? Description, string? CoverImageUrl);

public sealed record ReorderVoicesBody(IReadOnlyList<Guid>? VoiceIds);
