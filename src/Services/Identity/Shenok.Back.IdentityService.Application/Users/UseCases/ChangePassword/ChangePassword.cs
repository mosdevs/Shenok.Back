using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Domain.Services;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.ChangePassword;

public sealed record ChangePasswordBody(string? CurrentPassword, string NewPassword);

public sealed record ChangePasswordRequest(Guid UserId, string? CurrentPassword, string NewPassword);

public sealed class ChangePasswordBodyValidator : AbstractValidator<ChangePasswordBody>
{
    public ChangePasswordBodyValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]")
            .Matches("[a-z]")
            .Matches("[0-9]");
    }
}

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]")
            .Matches("[a-z]")
            .Matches("[0-9]");
    }
}

public sealed class ChangePasswordUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IUseCase<ChangePasswordRequest>
{
    public async Task ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(UserId.From(request.UserId), cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (user.PasswordHash is not null
            && (string.IsNullOrWhiteSpace(request.CurrentPassword)
                || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash.Value)))
        {
            throw new UnauthorizedException("The current password is not valid.");
        }

        user.ChangePassword(PasswordHash.FromHashedValue(passwordHasher.Hash(request.NewPassword)));
        await unitOfWork.SaveChanges(cancellationToken);
    }
}
