namespace Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

/// <summary>
/// Identity of one user-page relationship. Equality is the pair, not a separate Guid.
/// </summary>
public sealed record UserPageRelationshipId
{
    public UserId UserId { get; }
    public PageId PageId { get; }

    public UserPageRelationshipId(UserId userId, PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        UserId = userId;
        PageId = pageId;
    }

    public override string ToString() => $"{UserId}/{PageId}";
}
