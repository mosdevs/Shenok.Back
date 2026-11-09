using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Application.Playback.UseCases.OpenPlaylist;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class PlaylistsController(OpenPlaylistUseCase openPlaylist) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/audio/{voiceId:guid}/playlist.m3u8")]
    public async Task<IActionResult> Get(
        Guid voiceId,
        Guid waveformId,
        long exp,
        string? sig,
        CancellationToken cancellationToken)
    {
        var opened = await openPlaylist.ExecuteAsync(
            new OpenPlaylistRequest(voiceId, waveformId, exp, sig ?? string.Empty),
            cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Content(opened.Playlist, "application/vnd.apple.mpegurl");
    }
}
