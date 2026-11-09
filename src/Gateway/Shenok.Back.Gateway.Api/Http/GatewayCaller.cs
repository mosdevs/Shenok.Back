using System.Security.Claims;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Api.Security;
using Shenok.Back.Gateway.Application.Common;

namespace Shenok.Back.Gateway.Api.Http;

internal static class GatewayCaller
{
    public static Guid RequireUserId(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var subject = user.FindFirst(GatewayClaims.Subject)?.Value;
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedException("The access token subject is missing.");

        return userId;
    }

    public static bool IsAdmin(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.IsInRole(GatewayLimits.AdminRole);
    }
}
