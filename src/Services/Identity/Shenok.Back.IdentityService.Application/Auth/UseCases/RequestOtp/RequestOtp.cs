using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth.UseCases.RequestOtp;

public sealed record RequestOtpRequest(string? Email, string? PhoneNumber, string Purpose);

public sealed class RequestOtpValidator : AbstractValidator<RequestOtpRequest>
{
    public RequestOtpValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Email or phone number is required.");
        RuleFor(x => x.Purpose).Must(purpose => purpose is "register" or "login")
            .WithMessage("Purpose must be register or login.");
    }
}

public sealed class RequestOtpUseCase(
    IUserRepository users,
    IOtpChallengeRepository challenges,
    IUnitOfWork unitOfWork,
    OtpIssuer otpIssuer,
    TimeProvider clock) : IUseCase<RequestOtpRequest>
{
    private const string ActiveCodeMessage = "A verification code was already sent.";

    public async Task ExecuteAsync(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        var user = await FindAsync(request, users, cancellationToken);
        var purpose = request.Purpose == "login" ? OtpPurpose.Login : OtpPurpose.Register;
        if (purpose == OtpPurpose.Login && user.VerificationStatus != VerificationStatus.Verified)
            throw new ForbiddenException("An unverified account cannot request a login code.");

        if (purpose == OtpPurpose.Register && user.VerificationStatus == VerificationStatus.Verified)
            throw new ConflictException(AlreadyRegisteredMessage(request.PhoneNumber));

        var channel = OtpRouting.ResolveChannel(request.Email, request.PhoneNumber);
        if (purpose == OtpPurpose.Register)
            await EnsureNoActiveRegisterCodeAsync(user, channel, cancellationToken);

        var pending = await otpIssuer.StageAsync(user, purpose, cancellationToken, channel);
        await unitOfWork.SaveChanges(cancellationToken);
        await otpIssuer.DeliverAsync(pending, cancellationToken);
    }

    internal static string AlreadyRegisteredMessage(string? phoneNumber) =>
        string.IsNullOrWhiteSpace(phoneNumber)
            ? "Email is already registered."
            : "Phone number is already registered.";

    private async Task EnsureNoActiveRegisterCodeAsync(User user, OtpChannel channel, CancellationToken cancellationToken)
    {
        var destination = OtpRouting.ResolveDestination(user, channel);
        var active = await challenges.GetActiveAsync(
            user.Id,
            OtpPurpose.Register,
            destination,
            clock.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (active is not null)
            throw new ConflictException(ActiveCodeMessage);
    }

    internal static async Task<User> FindAsync(RequestOtpRequest request, IUserRepository users, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phone = PhoneNumber.Create(request.PhoneNumber);
            return await users.GetByPhoneAsync(phone, cancellationToken)
                ?? throw new NotFoundException("No account matches that phone number.");
        }

        var email = Email.Create(request.Email!);
        return await users.GetByEmailAsync(email, cancellationToken)
            ?? throw new NotFoundException("No account matches that email.");
    }
}
