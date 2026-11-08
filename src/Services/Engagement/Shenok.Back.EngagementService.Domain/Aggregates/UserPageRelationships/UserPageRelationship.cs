using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

/// <summary>
/// One user's relationship with one page. Follow and unlock are independent.
/// Subscriber count is not stored here.
/// </summary>
public sealed class UserPageRelationship : AggregateRoot<UserPageRelationshipId>
{
    public UserId UserId { get; private set; } = null!;
    public PageId PageId { get; private set; } = null!;
    public bool IsFollowing { get; private set; }
    public DateTime? FollowedAt { get; private set; }
    public DateTime? UnlockedAt { get; private set; }

    private UserPageRelationship()
    {
    }

    private UserPageRelationship(UserId userId, PageId pageId)
    {
        UserId = userId;
        PageId = pageId;
    }

    /// <summary>
    /// Starts a relationship with neither follow nor unlock. Raises no domain event.
    /// A missing pair is not created here; the use case decides when to call this.
    /// </summary>
    public static UserPageRelationship Start(UserId userId, PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        return new UserPageRelationship(userId, pageId);
    }

    /// <summary>
    /// Sets <see cref="UnlockedAt"/> once. A second call returns <see langword="false"/> and keeps the original timestamp.
    /// </summary>
    public bool Unlock()
    {
        if (UnlockedAt is not null)
            return false;

        UnlockedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PageUnlockedDomainEvent(UserId, PageId));
        return true;
    }

    /// <summary>
    /// Follows the page. Does not read or write <see cref="UnlockedAt"/>.
    /// Returns <see langword="false"/> when the user is already following.
    /// </summary>
    public bool Follow()
    {
        if (IsFollowing)
            return false;

        IsFollowing = true;
        FollowedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PageFollowedDomainEvent(UserId, PageId));
        return true;
    }

    /// <summary>
    /// Clears follow. Does not clear <see cref="UnlockedAt"/>.
    /// Returns <see langword="false"/> when the user is not following.
    /// </summary>
    public bool Unfollow()
    {
        if (!IsFollowing)
            return false;

        IsFollowing = false;
        FollowedAt = null;
        RaiseDomainEvent(new PageUnfollowedDomainEvent(UserId, PageId));
        return true;
    }

    /// <summary>
    /// Rebuilt from <see cref="UserId"/> and <see cref="PageId"/>.
    /// Persistence ignores this property and uses those two columns as the primary key.
    /// </summary>
    public override UserPageRelationshipId Id
    {
        get => new(UserId, PageId);
        protected set
        {
            ArgumentNullException.ThrowIfNull(value);
            UserId = value.UserId;
            PageId = value.PageId;
        }
    }
}
