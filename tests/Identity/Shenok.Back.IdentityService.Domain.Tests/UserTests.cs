using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Tests;

public class UserTests
{
    [Fact]
    public void Register_requires_email_or_phone()
    {
        var profile = UserProfile.Create("listener", "Listener");
        Assert.Throws<IdentifierRequiredException>(() => User.Register(null, null, null, profile));
    }

    [Fact]
    public void Email_account_requires_a_password()
    {
        var profile = UserProfile.Create("listener", "Listener");
        var email = Email.Create("listener@example.com");
        Assert.Throws<PasswordRequiredException>(() => User.Register(email, null, null, profile));
    }

    [Fact]
    public void Phone_account_can_omit_a_password()
    {
        var user = User.Register(null, PhoneNumber.Create("09123456789"), null, UserProfile.Create("listener", "Listener"));
        Assert.Null(user.PasswordHash);
        Assert.Equal("+989123456789", user.PhoneNumber!.Value);
        Assert.Equal(VerificationStatus.Unverified, user.VerificationStatus);
        Assert.Contains(UserRole.Listener, user.Roles);
    }

    [Theory]
    [InlineData("09123456789", "+989123456789")]
    [InlineData("9123456789", "+989123456789")]
    [InlineData("+989123456789", "+989123456789")]
    [InlineData("00989123456789", "+989123456789")]
    [InlineData("989123456789", "+989123456789")]
    public void Iranian_mobile_normalizes_to_e164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Create(input).Value);
    }

    [Fact]
    public void Verify_is_once_only()
    {
        var user = VerifiedPhoneUser();
        Assert.Throws<UserAlreadyVerifiedException>(() => user.Verify());
    }

    [Fact]
    public void GrantRole_makes_a_creator_a_listener_too()
    {
        var user = User.Register(null, PhoneNumber.Create("09120000000"), null, UserProfile.Create("creator", "Creator"));
        user.GrantRole(UserRole.ContentCreator);
        Assert.Contains(UserRole.ContentCreator, user.Roles);
        Assert.Contains(UserRole.Listener, user.Roles);
    }

    [Fact]
    public void Password_hash_text_does_not_reveal_the_hash()
    {
        var hash = PasswordHash.FromHashedValue("secret-hash-value");
        Assert.DoesNotContain("secret-hash-value", hash.ToString(), StringComparison.Ordinal);
    }

    private static User VerifiedPhoneUser()
    {
        var user = User.Register(null, PhoneNumber.Create("09121111111"), null, UserProfile.Create("ready", "Ready"));
        user.MarkVerificationPending();
        user.Verify();
        return user;
    }
}
