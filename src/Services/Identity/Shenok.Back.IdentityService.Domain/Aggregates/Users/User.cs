using Shenok.Back.IdentityService.Domain.DomainEvents;
using Shenok.Back.IdentityService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users
{
    public sealed class User : AggregateRoot<UserId>
    {
        public Email Email { get; private set; }
        public PasswordHash PasswordHash { get; private set; }
        public UserProfile Profile { get; private set; }
        public VerificationStatus VerificationStatus { get; private set; }
        public UserRole UserRole { get; private set; }
        public DateTime CreatedAt { get; private set; }
        private User() { } //for EF core. it must not be
                           // usable by application code, Must be private

        private User(
            UserId id, //?
            Email email,
            PasswordHash passwordHash,
            UserProfile profile,
            UserRole userRole) : base(id)
        {

            Email = email;
            PasswordHash = passwordHash;
            Profile = profile;
            UserRole = userRole;
            CreatedAt = DateTime.UtcNow;
        }
        //Factory methods:
        public static User Register(
            Email email,
            PasswordHash passwordHash,
            UserProfile profile,
            UserRole role)
        {
            var user = new User(UserId.New(), email, passwordHash, profile, role);
            user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, user.Email.Value));
            return user;
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
    }
}
