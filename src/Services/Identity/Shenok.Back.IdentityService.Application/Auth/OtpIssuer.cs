using Shenok.Back.Application.Abstractions.Security;
using Shenok.Back.IdentityService.Application.Common;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Auth;

public sealed record PendingOtp(OtpChannel Channel, string Destination, string Code);

public sealed class OtpIssuer(
    IOtpChallengeRepository challenges,
    ISmsSender smsSender,
    IEmailSender emailSender,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<PendingOtp>> StageAsync(
        User user,
        OtpPurpose purpose,
        CancellationToken cancellationToken,
        OtpChannel? channel = null)
    {
        if (user.VerificationStatus != VerificationStatus.Verified)
            user.MarkVerificationPending();

        var pending = new List<PendingOtp>();
        if ((channel is null or OtpChannel.Phone) && user.PhoneNumber is not null)
            pending.Add(await StageOneAsync(user, purpose, OtpChannel.Phone, user.PhoneNumber.Value, cancellationToken));

        if ((channel is null or OtpChannel.Email) && user.Email is not null)
            pending.Add(await StageOneAsync(user, purpose, OtpChannel.Email, user.Email.Value, cancellationToken));

        return pending;
    }

    public async Task DeliverAsync(IReadOnlyList<PendingOtp> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            if (message.Channel == OtpChannel.Phone)
                await smsSender.SendVerificationCodeAsync(message.Destination, message.Code, cancellationToken);
            else
                await emailSender.SendAsync(message.Destination, "Shenok verification code", $"Shenok verification code: {message.Code}", cancellationToken);
        }
    }

    private async Task<PendingOtp> StageOneAsync(
        User user,
        OtpPurpose purpose,
        OtpChannel channel,
        string destination,
        CancellationToken cancellationToken)
    {
        var code = OtpCodeFactory.Create();
        var challenge = OtpChallenge.Issue(
            user.Id,
            Sha256Hasher.Hash(code),
            purpose,
            channel,
            destination,
            clock.GetUtcNow().UtcDateTime,
            OtpCodeFactory.Lifetime);
        await challenges.AddAsync(challenge, cancellationToken);
        return new PendingOtp(channel, destination, code);
    }
}
