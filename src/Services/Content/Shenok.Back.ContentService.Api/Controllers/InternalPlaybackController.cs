using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.ContentService.Api.Http;
using Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;
using Shenok.Back.ContentService.Application.Playback.UseCases.OpenVoiceSegment;

namespace Shenok.Back.ContentService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalPlaybackController(
    IssuePlaylistUseCase issuePlaylist,
    OpenVoiceSegmentUseCase openVoiceSegment) : ControllerBase
{
    private const string SegmentMediaType = "video/mp2t";

    [HttpPost("/internal/playback")]
    public Task<IActionResult> Issue(IssuePlaylistBody body, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new IssuePlaylistRequest(body.VoiceId, body.WaveformId),
            (request, token) => issuePlaylist.ExecuteAsync(request, token),
            cancellationToken);

    [HttpGet("/internal/voices/{voiceId:guid}/segments/{*path}")]
    public async Task<IActionResult> OpenSegment(Guid voiceId, string? path, CancellationToken cancellationToken)
    {
        var request = new OpenVoiceSegmentRequest(voiceId, path);
        var invalid = await UseCaseHttp.ProblemAsync(HttpContext, request, cancellationToken);
        if (invalid is not null)
            return invalid;

        var opened = await openVoiceSegment.ExecuteAsync(request, cancellationToken);
        Response.ContentLength = opened.Length;
        return new FileStreamResult(opened.Content, SegmentMediaType)
        {
            EnableRangeProcessing = false
        };
    }
}

public sealed record IssuePlaylistBody(Guid VoiceId, Guid? WaveformId);
