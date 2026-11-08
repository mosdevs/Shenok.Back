using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;

namespace Shenok.Back.EngagementService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalCanPlayController(
    CanPlayUseCase canPlay,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/internal/can-play")]
    public Task<IActionResult> Check(CanPlayBody? body, CancellationToken cancellationToken)
    {
        var playback = body ?? new CanPlayBody(Guid.Empty);
        return UseCaseHttp.OkAsync(
            HttpContext,
            new CanPlayRequest(caller.GetRequired().UserId, playback.PageId),
            (request, token) => canPlay.ExecuteAsync(request, token),
            cancellationToken);
    }
}

public sealed record CanPlayBody(Guid PageId);
