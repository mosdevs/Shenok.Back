using Shenok.Back.Application.Abstractions.Security;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Tests;

public class OtpChallengeTests
{
    private static readonly UserId UserId = UserId.From(Guid.NewGuid());
    private static readonly DateTime IssuedAt = new(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Match_succeeds_for_the_correct_code()
    {
        var challenge = Issue("123456");
        challenge.Match(Sha256Hasher.Hash("123456"), IssuedAt.AddSeconds(30));
        Assert.True(challenge.IsConsumed);
        Assert.Equal(1, challenge.AttemptCount);
    }

    [Fact]
    public void Match_rejects_an_expired_challenge()
    {
        var challenge = Issue("123456");
        Assert.Throws<OtpChallengeFailedException>(() =>
            challenge.Match(Sha256Hasher.Hash("123456"), IssuedAt.AddMinutes(5)));
    }

    [Fact]
    public void Match_rejects_after_max_attempts()
    {
        var challenge = Issue("123456");
        for (var attempt = 0; attempt < OtpChallenge.MaxAttempts; attempt++)
        {
            Assert.Throws<OtpChallengeFailedException>(() =>
                challenge.Match(Sha256Hasher.Hash("000000"), IssuedAt.AddSeconds(attempt)));
        }

        Assert.Throws<OtpChallengeFailedException>(() =>
            challenge.Match(Sha256Hasher.Hash("123456"), IssuedAt.AddSeconds(10)));
    }

    [Fact]
    public void Match_rejects_a_consumed_challenge()
    {
        var challenge = Issue("123456");
        challenge.Match(Sha256Hasher.Hash("123456"), IssuedAt.AddSeconds(1));
        Assert.Throws<OtpChallengeFailedException>(() =>
            challenge.Match(Sha256Hasher.Hash("123456"), IssuedAt.AddSeconds(2)));
    }

    private static OtpChallenge Issue(string code) =>
        OtpChallenge.Issue(
            UserId,
            Sha256Hasher.Hash(code),
            OtpPurpose.Register,
            OtpChannel.Phone,
            "+989123456789",
            IssuedAt,
            TimeSpan.FromMinutes(2));
}
