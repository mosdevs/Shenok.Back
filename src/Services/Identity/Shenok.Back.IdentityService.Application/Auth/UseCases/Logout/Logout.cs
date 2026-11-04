using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth.UseCases.Logout;

public sealed record LogoutRequest(string RefreshToken);

public sealed class LogoutValidator : AbstractValidator<LogoutRequest>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class LogoutUseCase(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IUseCase<LogoutRequest>
{
    public async Task ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var existing = await refreshTokens.GetByHashAsync(tokenService.Hash(request.RefreshToken), cancellationToken)
            ?? throw new UnauthorizedException("The refresh token is not valid.");
        if (!existing.IsRevoked)
            existing.Revoke();
        await unitOfWork.SaveChanges(cancellationToken);
    }
}
