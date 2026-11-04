using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shenok.Back.Application.Abstractions.Security;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "shenok";
    public string Audience { get; set; } = "shenok-mobile";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(options.Value.RefreshTokenDays);

    public (string AccessToken, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var settings = options.Value;
        if (settings.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var expiresAt = clock.GetUtcNow().UtcDateTime.AddMinutes(settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new("username", user.Profile.Username),
            new("verified", user.VerificationStatus == VerificationStatus.Verified ? "true" : "false")
        };
        claims.AddRange(user.Roles.Select(role => new Claim("roles", role.ToString())));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: expiresAt, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string CreateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string Hash(string value) => Sha256Hasher.Hash(value);
}
