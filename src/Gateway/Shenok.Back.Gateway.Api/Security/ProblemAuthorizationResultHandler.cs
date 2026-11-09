using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Shenok.Back.Gateway.Api.Security;

/// <summary>
/// Turns authentication and authorization failures into the shared problem document.
/// A missing or invalid token is 401. A token whose <c>verified</c> claim is not <c>true</c> is 403.
/// The response never includes the token or the framework's validation text.
/// </summary>
internal sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        if (context.Response.HasStarted)
            return;

        if (authorizeResult.Challenged)
        {
            if (policy.AuthenticationSchemes.Count > 0)
            {
                foreach (var scheme in policy.AuthenticationSchemes)
                    await context.ChallengeAsync(scheme);
            }
            else
            {
                await context.ChallengeAsync();
            }

            return;
        }

        var verified = context.User.FindAll(GatewayClaims.Verified)
            .Any(claim => claim.Value == GatewayClaims.VerifiedTrue);
        await GatewayAuthResponses.WriteForbiddenAsync(
            context,
            verified ? GatewayAuthResponses.NotAllowed : GatewayAuthResponses.NotVerified);
    }
}
