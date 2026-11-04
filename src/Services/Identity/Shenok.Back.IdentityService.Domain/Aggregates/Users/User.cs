using Shenok.Back.IdentityService.Domain.DomainEvents;
using Shenok.Back.IdentityService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users;

public sealed class User : AggregateRoot<UserId>
{
    private readonly List<UserRole> _roles = [];

    public Email? Email { get; private set; }
    public PhoneNumber? PhoneNumber { get; private set; }
    public PasswordHash? PasswordHash { get; private set; }
    public UserProfile Profile { get; private set; } = null!;
    public VerificationStatus VerificationStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<UserRole> Roles => _roles;

    private string RolesStorage
    {
        get => string.Join(',', _roles.Select(role => role.ToString()));
        set
        {
            _roles.Clear();
            if (string.IsNullOrWhiteSpace(value))
                return;

            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                _roles.Add(Enum.Parse<UserRole>(part));
        }
    }

    private User()
    {
    }

    private User(
        UserId id,
        Email? email,
        PhoneNumber? phoneNumber,
        PasswordHash? passwordHash,
        UserProfile profile) : base(id)
    {
        Email = email;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Profile = profile;
        VerificationStatus = VerificationStatus.Unverified;
        CreatedAt = DateTime.UtcNow;
        _roles.Add(UserRole.Listener);
    }

    public static User Register(
        Email? email,
        PhoneNumber? phoneNumber,
        PasswordHash? passwordHash,
        UserProfile profile)
    {
        if (email is null && phoneNumber is null)
            throw new IdentifierRequiredException();

        if (email is not null && phoneNumber is null && passwordHash is null)
            throw new PasswordRequiredException();

        var user = new User(UserId.New(), email, phoneNumber, passwordHash, profile);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Email?.Value, user.PhoneNumber?.Value));
        return user;
    }

    public void MarkVerificationPending()
    {
        if (VerificationStatus == VerificationStatus.Verified)
            throw new UserAlreadyVerifiedException(Id);

        VerificationStatus = VerificationStatus.Pending;
    }

    public void Verify()
    {
        if (VerificationStatus == VerificationStatus.Verified)
            throw new UserAlreadyVerifiedException(Id);

        VerificationStatus = VerificationStatus.Verified;
        RaiseDomainEvent(new UserVerifiedDomainEvent(Id));
    }

    public void UpdateProfile(UserProfile newProfile)
    {
        Profile = newProfile;
        RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id));
    }

    public void ChangePassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    public void GrantRole(UserRole role)
    {
        AddRole(role);
        if (role is UserRole.ContentCreator or UserRole.Admin)
            AddRole(UserRole.Listener);
    }

    private void AddRole(UserRole role)
    {
        if (!_roles.Contains(role))
            _roles.Add(role);
    }
}
