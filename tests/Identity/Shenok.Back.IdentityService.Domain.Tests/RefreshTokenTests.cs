using Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly UserId UserId = UserId.From(Guid.NewGuid());
    private static readonly DateTime ExpiresAt = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Revoke_with_rotation_sets_replaced_by()
    {
        var token = RefreshToken.Issue("hash", UserId, ExpiresAt);
        var replacement = RefreshTokenId.New();
        token.Revoke(replacement);
        Assert.True(token.IsRevoked);
        Assert.Equal(replacement.Value, token.ReplacedById);
    }

    [Fact]
    public void Revoke_without_rotation_marks_revoked_only()
    {
        var token = RefreshToken.Issue("hash", UserId, ExpiresAt);
        token.Revoke();
        Assert.True(token.IsRevoked);
        Assert.Null(token.ReplacedById);
    }

    [Fact]
    public void Double_revoke_with_rotation_throws()
    {
        var token = RefreshToken.Issue("hash", UserId, ExpiresAt);
        token.Revoke(RefreshTokenId.New());
        Assert.Throws<RefreshTokenAlreadyRevokedException>(() => token.Revoke(RefreshTokenId.New()));
    }
}
