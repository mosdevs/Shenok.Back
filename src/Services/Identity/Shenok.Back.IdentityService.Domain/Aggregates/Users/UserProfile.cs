namespace Shenok.Back.IdentityService.Domain.Aggregates.Users
{
    public sealed record UserProfile
    {
        /*
         * These 3 props are grouped into one Value Object rather than being three separate properties directly on User,
         * because conceptually they change together as one action — "update my profile" —
         * */
        public string DisplayName { get; }
        public string? AvatarUrl { get; }
        public string? Bio { get; }

        private UserProfile(
            string displayName,
            string? avatarUrl,
            string? bio)
        {
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
            Bio = bio;
        }
        public static UserProfile Create(
            string displayName,
            string? avatarUrl = null,
            string? bio = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Display name cannot be empty!", nameof(displayName));
            if(displayName.Length > 50)
                throw new ArgumentException("Display name is too big. please enter a string with less than 50 char.",nameof(displayName));
            return new UserProfile(displayName.Trim(), avatarUrl, bio);
        }
        /*
         * Value objects are immutable. when we need to update them, a new one MUST be created and replaced.
         * With these 3 methods: when User.UpdateProfile(...) called:
         * a new objects with each created.
         **/
        public UserProfile WithDisplayName(string newDisplayName) => Create(newDisplayName, AvatarUrl, Bio);
        public UserProfile WithAvatarUrl(string? newAvatarUrl) => Create(DisplayName, newAvatarUrl, Bio);
        public UserProfile WithBio(string? newBio) => Create(DisplayName, AvatarUrl, newBio);

    }
}
