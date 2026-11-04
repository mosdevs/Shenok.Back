using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Domain.Services;

namespace Shenok.Back.IdentityService.Application.Auth.UseCases.Login;

public sealed record LoginRequest(string? Email, string? PhoneNumber, string Password);

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Email or phone number is required.");
    }
}

public sealed class LoginUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TokenIssuer tokenIssuer) : IUseCase<LoginRequest, AuthTokens>
{
    public async Task<AuthTokens> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await FindAsync(request, users, cancellationToken);
        if (user.VerificationStatus != VerificationStatus.Verified)
            throw new ForbiddenException("An unverified account cannot sign in.");
        if (user.PasswordHash is null || !passwordHasher.Verify(request.Password, user.PasswordHash.Value))
            throw new UnauthorizedException("The credentials are not valid.");

        var issued = await tokenIssuer.IssueAsync(user, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return issued.Tokens;
    }

    private static async Task<User> FindAsync(LoginRequest request, IUserRepository users, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phone = PhoneNumber.Create(request.PhoneNumber);
            return await users.GetByPhoneAsync(phone, cancellationToken)
                ?? throw new UnauthorizedException("The credentials are not valid.");
        }

        var email = Email.Create(request.Email!);
        return await users.GetByEmailAsync(email, cancellationToken)
            ?? throw new UnauthorizedException("The credentials are not valid.");
    }
}
