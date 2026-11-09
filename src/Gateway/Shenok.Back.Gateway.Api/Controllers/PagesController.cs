using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Follows.UseCases.FollowPage;
using Shenok.Back.Gateway.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.Gateway.Application.Pages.UseCases.OpenPage;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class PagesController(
    OpenPageUseCase openPage,
    FollowPageUseCase followPage,
    UnfollowPageUseCase unfollowPage) : ControllerBase
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
}
