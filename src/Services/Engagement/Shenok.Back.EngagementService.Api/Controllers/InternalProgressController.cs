using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Listening.UseCases.ReportProgress;

namespace Shenok.Back.EngagementService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalProgressController(
    ReportProgressUseCase reportProgress,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/internal/progress")]
    public Task<IActionResult> Report(ReportProgressBody? body, CancellationToken cancellationToken)
    {
        var progress = body ?? new ReportProgressBody(Guid.Empty, null, 0, false, false);
        return UseCaseHttp.OkAsync(
            HttpContext,
            new ReportProgressRequest(
                caller.GetRequired().UserId,
                progress.VoiceId,
                progress.PlaySessionId ?? string.Empty,
                progress.PositionSeconds,
                progress.Completed,
                progress.IsSeek),
            (request, token) => reportProgress.ExecuteAsync(request, token),
            cancellationToken);
    }
}

/// <summary>
/// Progress body. <c>playSessionId</c> is the client primary key. The body cannot name the user.
/// </summary>
public sealed record ReportProgressBody(
    Guid VoiceId,
    string? PlaySessionId,
    int PositionSeconds,
    bool Completed,
    bool IsSeek);
