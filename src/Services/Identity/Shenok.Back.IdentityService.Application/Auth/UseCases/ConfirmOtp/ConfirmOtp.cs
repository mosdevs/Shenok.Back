using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Application.Abstractions.Security;
using Shenok.Back.IdentityService.Application.Common;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth.UseCases.ConfirmOtp;

public sealed record ConfirmOtpRequest(string? Email, string? PhoneNumber, string Code, string Purpose);

public sealed class ConfirmOtpValidator : AbstractValidator<ConfirmOtpRequest>
{
    public ConfirmOtpValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6);
        RuleFor(x => x.Purpose).Must(purpose => purpose is "register" or "login");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Email or phone number is required.");
    }
}

public sealed class ConfirmOtpUseCase(
    IUserRepository users,
    IOtpChallengeRepository challenges,
    IUnitOfWork unitOfWork,
    TokenIssuer tokenIssuer,
    TimeProvider clock) : IUseCase<ConfirmOtpRequest, AuthTokens>
{
    public async Task<AuthTokens> ExecuteAsync(ConfirmOtpRequest request, CancellationToken cancellationToken)
    {
        var lookup = new RequestOtp.RequestOtpRequest(request.Email, request.PhoneNumber, request.Purpose);
        var user = await RequestOtp.RequestOtpUseCase.FindAsync(lookup, users, cancellationToken);
        var purpose = request.Purpose == "login" ? OtpPurpose.Login : OtpPurpose.Register;
        if (purpose == OtpPurpose.Login && user.VerificationStatus != VerificationStatus.Verified)
            throw new ForbiddenException("An unverified account cannot sign in with a login code.");
        if (purpose == OtpPurpose.Register && user.VerificationStatus == VerificationStatus.Verified)
            throw new ConflictException(RequestOtp.RequestOtpUseCase.AlreadyRegisteredMessage(request.PhoneNumber));

        var utcNow = clock.GetUtcNow().UtcDateTime;
        var channel = OtpRouting.ResolveChannel(request.Email, request.PhoneNumber);
        var destination = OtpRouting.ResolveDestination(user, channel);
        var challenge = await challenges.GetActiveAsync(user.Id, purpose, destination, utcNow, cancellationToken)
            ?? throw new NotFoundException("No active verification code exists.");

        challenge.Match(Sha256Hasher.Hash(request.Code), utcNow);
        if (purpose == OtpPurpose.Register)
            user.Verify();

        var issued = await tokenIssuer.IssueAsync(user, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return issued.Tokens;
    }
}
