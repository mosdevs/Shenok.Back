using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth;

public sealed record IssuedSession(AuthTokens Tokens, RefreshToken RefreshToken);

public sealed class TokenIssuer(ITokenService tokenService, IRefreshTokenRepository refreshTokens, TimeProvider clock)
{
    public async Task<IssuedSession> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var refreshValue = tokenService.CreateRefreshTokenValue();
        var refresh = RefreshToken.Issue(
            tokenService.Hash(refreshValue),
            user.Id,
            clock.GetUtcNow().UtcDateTime.Add(tokenService.RefreshTokenLifetime));
        await refreshTokens.AddAsync(refresh, cancellationToken);
        var (accessToken, expiresAt) = tokenService.CreateAccessToken(user);
        return new IssuedSession(new AuthTokens(accessToken, refreshValue, expiresAt), refresh);
    }
}
