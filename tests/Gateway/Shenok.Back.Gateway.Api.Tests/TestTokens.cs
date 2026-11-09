using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Shenok.Back.Gateway.Api;
using Shenok.Back.Gateway.Api.Security;

namespace Shenok.Back.Gateway.Api.Tests;

internal static class TestTokens
{
    public static string Create(
        Guid userId,
        string? verified = GatewayClaims.VerifiedTrue,
        IReadOnlyList<string>? roles = null,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        DateTime? expires = null,
        bool includeSubject = true,
        string? subject = null)
    {
        var claims = new List<Claim> { new(GatewayClaims.Username, "listener") };
        if (includeSubject)
            claims.Add(new Claim(GatewayClaims.Subject, subject ?? userId.ToString("D")));
        if (verified is not null)
            claims.Add(new Claim(GatewayClaims.Verified, verified));
        if (roles is not null)
            claims.AddRange(roles.Select(role => new Claim(GatewayClaims.Roles, role)));

        var key = signingKey ?? GatewayTestSettings.SigningKey;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var token = new JwtSecurityToken(
            issuer ?? GatewayConfiguration.DefaultJwtIssuer,
            audience ?? GatewayConfiguration.DefaultJwtAudience,
            claims,
            notBefore: expiresAt.AddMinutes(-10),
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
