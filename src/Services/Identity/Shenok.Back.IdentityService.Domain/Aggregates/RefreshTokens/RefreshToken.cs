using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;

public sealed record RefreshTokenId
{
    public Guid Value { get; private set; }
    private RefreshTokenId() { }
    private RefreshTokenId(Guid value) => Value = value;
    public static RefreshTokenId New() => new(Guid.NewGuid());
    public static RefreshTokenId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Refresh token id cannot be empty.", nameof(value))
        : new RefreshTokenId(value);
}

public sealed class RefreshToken : AggregateRoot<RefreshTokenId>
{
    public string TokenHash { get; private set; } = string.Empty;
    public UserId UserId { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public Guid? ReplacedById { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(RefreshTokenId id, string tokenHash, UserId userId, DateTime expiresAt) : base(id)
    {
        TokenHash = tokenHash;
        UserId = userId;
        ExpiresAt = expiresAt;
    }

    public static RefreshToken Issue(string tokenHash, UserId userId, DateTime expiresAt) =>
        new(RefreshTokenId.New(), tokenHash, userId, expiresAt);

    public void Revoke(RefreshTokenId replacedBy)
    {
        if (IsRevoked)
            throw new RefreshTokenAlreadyRevokedException();

        IsRevoked = true;
        ReplacedById = replacedBy.Value;
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
