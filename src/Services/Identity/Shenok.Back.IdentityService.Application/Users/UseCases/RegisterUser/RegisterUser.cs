using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Auth;
using Shenok.Back.IdentityService.Application.Common;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Domain.Services;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUser;

public sealed record RegisterUserRequest(
    string Username,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string? Password,
    string? AvatarUrl,
    string? Bio);

public sealed record RegisterUserResponse(Guid UserId, string Username, string VerificationStatus);

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber).MaximumLength(20);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Email or phone number is required.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .When(x => !string.IsNullOrWhiteSpace(x.Email) && string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.Password)
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public sealed class RegisterUserUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    OtpIssuer otpIssuer) : IUseCase<RegisterUserRequest, RegisterUserResponse>
{
    public async Task<RegisterUserResponse> ExecuteAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : Email.Create(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : PhoneNumber.Create(request.PhoneNumber);
        var profile = UserProfile.Create(request.Username, request.DisplayName, request.AvatarUrl, request.Bio);

        if (email is not null && await users.ExistsByEmailAsync(email, cancellationToken))
            throw new ConflictException("Email is already registered.");
        if (phone is not null && await users.ExistsByPhoneAsync(phone, cancellationToken))
            throw new ConflictException("Phone number is already registered.");
        if (await users.ExistsByUsernameAsync(profile.Username, cancellationToken))
            throw new ConflictException("Username is already registered.");

        PasswordHash? password = string.IsNullOrWhiteSpace(request.Password)
            ? null
            : PasswordHash.FromHashedValue(passwordHasher.Hash(request.Password));

        var user = User.Register(email, phone, password, profile);
        await users.AddAsync(user, cancellationToken);
        var channel = OtpRouting.RegistrationChannel(email, phone);
        var pending = await otpIssuer.StageAsync(user, OtpPurpose.Register, cancellationToken, channel);
        await unitOfWork.SaveChanges(cancellationToken);
        await otpIssuer.DeliverAsync(pending, cancellationToken);
        return new RegisterUserResponse(user.Id.Value, user.Profile.Username, user.VerificationStatus.ToString());
    }
}
