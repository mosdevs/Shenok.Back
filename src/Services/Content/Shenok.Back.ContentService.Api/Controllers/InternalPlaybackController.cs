using Microsoft.AspNetCore.Mvc;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
public sealed class InternalPlaybackController(IssuePlaylistUseCase issuePlaylist) : ControllerBase
{
    [HttpPost("/internal/playback")]
    public Task<IActionResult> Issue(IssuePlaylistBody body, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new IssuePlaylistRequest(body.VoiceId, body.WaveformId),
            (request, token) => issuePlaylist.ExecuteAsync(request, token),
            cancellationToken);
}

public sealed record IssuePlaylistBody(Guid VoiceId, Guid? WaveformId);
