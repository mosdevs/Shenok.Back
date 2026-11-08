using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;
using Shenok.Back.EngagementService.Application.Follows.UseCases.UnfollowPage;

namespace Shenok.Back.EngagementService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalFollowsController(
    FollowPageUseCase followPage,
    UnfollowPageUseCase unfollowPage,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/internal/pages/{pageId:guid}/follow")]
    public Task<IActionResult> Follow(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new FollowPageRequest(caller.GetRequired().UserId, pageId),
            (request, token) => followPage.ExecuteAsync(request, token),
            cancellationToken);

    [HttpDelete("/internal/pages/{pageId:guid}/follow")]
    public Task<IActionResult> Unfollow(Guid pageId, CancellationToken cancellationToken) =>
        UseCaseHttp.OkAsync(
            HttpContext,
            new UnfollowPageRequest(caller.GetRequired().UserId, pageId),
            (request, token) => unfollowPage.ExecuteAsync(request, token),
            cancellationToken);
}
