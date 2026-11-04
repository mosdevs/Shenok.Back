using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth.UseCases.RefreshSession;

public sealed record RefreshSessionRequest(string RefreshToken);

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionRequest>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshSessionUseCase(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    TokenIssuer tokenIssuer,
    TimeProvider clock) : IUseCase<RefreshSessionRequest, AuthTokens>
{
    public async Task<AuthTokens> ExecuteAsync(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        var existing = await refreshTokens.GetByHashAsync(tokenService.Hash(request.RefreshToken), cancellationToken)
            ?? throw new UnauthorizedException("The refresh token is not valid.");

        if (existing.IsRevoked || existing.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            throw new UnauthorizedException("The refresh token is not valid.");

        var user = await users.GetByIdAsync(existing.UserId, cancellationToken)
            ?? throw new UnauthorizedException("The refresh token is not valid.");
        if (user.VerificationStatus != Domain.Aggregates.Users.VerificationStatus.Verified)
            throw new ForbiddenException("An unverified account cannot refresh a session.");

        var issued = await tokenIssuer.IssueAsync(user, cancellationToken);
        existing.Revoke(issued.RefreshToken.Id);
        await unitOfWork.SaveChanges(cancellationToken);
        return issued.Tokens;
    }
}
