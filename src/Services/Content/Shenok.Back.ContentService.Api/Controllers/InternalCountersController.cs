using Microsoft.AspNetCore.Mvc;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Counters.UseCases.ApplySubscriberDelta;
using Shenok.Back.ContentService.Application.Counters.UseCases.IncrementPlayCount;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
public sealed class InternalCountersController(
    ApplySubscriberDeltaUseCase applySubscriberDelta,
    IncrementPlayCountUseCase incrementPlayCount) : ControllerBase
{
    [HttpPost("/internal/pages/{pageId:guid}/subscribers")]
    public Task<IActionResult> ApplySubscriberDelta(
        Guid pageId,
        SubscriberDeltaBody body,
        CancellationToken cancellationToken) =>
        UseCaseHttp.OkOrDeferredAsync(
            HttpContext,
            new ApplySubscriberDeltaRequest(pageId, body.Delta, body.IdempotencyKey ?? string.Empty),
            (request, token) => applySubscriberDelta.ExecuteAsync(request, token),
            cancellationToken);

    [HttpPost("/internal/voices/{voiceId:guid}/plays")]
    public Task<IActionResult> IncrementPlay(
        Guid voiceId,
        PlayCountBody body,
        CancellationToken cancellationToken) =>
        UseCaseHttp.OkOrDeferredAsync(
            HttpContext,
            new IncrementPlayCountRequest(voiceId, body.PlaySessionId),
            (request, token) => incrementPlayCount.ExecuteAsync(request, token),
            cancellationToken);
}

public sealed record SubscriberDeltaBody(long Delta, string? IdempotencyKey);

public sealed record PlayCountBody(Guid PlaySessionId);
