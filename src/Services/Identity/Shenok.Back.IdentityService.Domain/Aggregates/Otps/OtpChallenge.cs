using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Otps;

public enum OtpPurpose
{
    Register = 0,
    Login = 1
}

public enum OtpChannel
{
    Phone = 0,
    Email = 1
}

public sealed record OtpChallengeId
{
    public Guid Value { get; private set; }
    private OtpChallengeId() { }
    private OtpChallengeId(Guid value) => Value = value;
    public static OtpChallengeId New() => new(Guid.NewGuid());
    public static OtpChallengeId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Otp challenge id cannot be empty.", nameof(value))
        : new OtpChallengeId(value);
}

public sealed class OtpChallenge : AggregateRoot<OtpChallengeId>
{
    public const int MaxAttempts = 5;

    public UserId UserId { get; private set; } = null!;
    public string CodeHash { get; private set; } = string.Empty;
    public OtpPurpose Purpose { get; private set; }
    public OtpChannel Channel { get; private set; }
    public string Destination { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsConsumed { get; private set; }

    private OtpChallenge()
    {
    }

    private OtpChallenge(
        OtpChallengeId id,
        UserId userId,
        string codeHash,
        OtpPurpose purpose,
        OtpChannel channel,
        string destination,
        DateTime expiresAt) : base(id)
    {
        UserId = userId;
        CodeHash = codeHash;
        Purpose = purpose;
        Channel = channel;
        Destination = destination;
        ExpiresAt = expiresAt;
    }

    public static OtpChallenge Issue(
        UserId userId,
        string codeHash,
        OtpPurpose purpose,
        OtpChannel channel,
        string destination,
        DateTime utcNow,
        TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("OTP hash cannot be empty.", nameof(codeHash));

        return new OtpChallenge(OtpChallengeId.New(), userId, codeHash, purpose, channel, destination, utcNow.Add(lifetime));
    }

    public void Match(string submittedCodeHash, DateTime utcNow)
    {
        if (IsConsumed || utcNow > ExpiresAt || AttemptCount >= MaxAttempts)
            throw new OtpChallengeFailedException();

        AttemptCount++;
        if (!string.Equals(CodeHash, submittedCodeHash, StringComparison.Ordinal))
            throw new OtpChallengeFailedException();

        IsConsumed = true;
    }
}
