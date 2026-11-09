using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Listening.UseCases.ReportProgress;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class ProgressController(ReportProgressUseCase reportProgress) : ControllerBase
{
    [HttpPost("/voices/{voiceId:guid}/progress")]
    public Task<IActionResult> Report(Guid voiceId, ProgressBody? body, CancellationToken cancellationToken)
    {
        var progress = body ?? new ProgressBody(null, 0, false, false);
        return GatewayHttp.OkAsync(
            HttpContext,
            new ReportProgressRequest(
                GatewayCaller.RequireUserId(User),
                voiceId,
                progress.PlaySessionId ?? string.Empty,
                progress.PositionSeconds,
                progress.Completed,
                progress.IsSeek,
                GatewayCaller.IsAdmin(User)),
            (request, token) => reportProgress.ExecuteAsync(request, token),
            cancellationToken);
    }
}

/// <summary>
/// Progress body. The caller is the access token. A <c>userId</c> field in the JSON is ignored.
/// </summary>
public sealed record ProgressBody(string? PlaySessionId, int PositionSeconds, bool Completed, bool IsSeek);
