using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;

namespace Shenok.Back.EngagementService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalListeningController(
    GetListeningStateUseCase listeningState,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/internal/listening-state")]
    public Task<IActionResult> Get(ListeningStateBody? body, CancellationToken cancellationToken)
    {
        var state = body ?? new ListeningStateBody(Guid.Empty, null);
        return UseCaseHttp.OkAsync(
            HttpContext,
            new GetListeningStateRequest(
                caller.GetRequired().UserId,
                state.PageId,
                state.VoiceIds ?? []),
            (request, token) => listeningState.ExecuteAsync(request, token),
            cancellationToken);
    }
}

/// <summary>
/// A missing voice list is an empty list. The validator rejects a list longer than the cap.
/// </summary>
public sealed record ListeningStateBody(Guid PageId, IReadOnlyList<Guid>? VoiceIds);
