using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Security;

namespace Shenok.Back.Gateway.Api.Tests;

[ApiController]
public sealed class AuthProbeController : ControllerBase
{
    [HttpGet("~/probe")]
    public IActionResult Get() => Ok(Caller());

    [Authorize(Policy = GatewayPolicies.VerifiedCaller)]
    [HttpGet("~/probe/named")]
    public IActionResult Named() => Ok(Caller());

    private object Caller() => new
    {
        sub = User.FindFirst(GatewayClaims.Subject)?.Value,
        mappedSub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        verified = User.FindFirst(GatewayClaims.Verified)?.Value
    };
}
