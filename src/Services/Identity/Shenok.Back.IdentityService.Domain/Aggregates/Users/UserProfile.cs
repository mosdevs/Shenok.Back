using System.Text.RegularExpressions;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users;

public sealed record UserProfile
{
    private static readonly Regex UsernamePattern = new(@"^[a-z0-9_]{3,30}$", RegexOptions.Compiled);

    public string Username { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }

    private UserProfile()
    {
    }

    private UserProfile(string username, string displayName, string? avatarUrl, string? bio)
    {
        Username = username;
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        Bio = bio;
    }

    public static UserProfile Create(string username, string displayName, string? avatarUrl = null, string? bio = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidUsernameException("Username cannot be empty.");

        var normalizedUsername = username.Trim().ToLowerInvariant();
        if (!UsernamePattern.IsMatch(normalizedUsername))
            throw new InvalidUsernameException("Username must be 3-30 characters of lowercase letters, digits, or underscores.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidUsernameException("Display name cannot be empty.");

        var trimmedName = displayName.Trim();
        if (trimmedName.Length > 50)
            throw new InvalidUsernameException("Display name must be 50 characters or fewer.");

        if (bio is { Length: > 500 })
            throw new InvalidUsernameException("Bio must be 500 characters or fewer.");

        return new UserProfile(normalizedUsername, trimmedName, string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(), bio?.Trim());
    }

    public override string ToString() => Username;
}
