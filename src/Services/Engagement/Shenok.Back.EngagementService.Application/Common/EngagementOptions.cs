namespace Shenok.Back.EngagementService.Application.Common;

/// <summary>
/// Host settings for Engagement. Bind the <see cref="SectionName"/> section.
/// <see cref="AutoFollowOnScan"/> stays false unless a product decision turns the branch on.
/// </summary>
public sealed class EngagementOptions
{
    public const string SectionName = "Engagement";

    /// <summary>
    /// When true, <c>RegisterScan</c> follows the page after unlocking it and then applies a subscriber delta of +1.
    /// The default is false: a scan unlocks, leaves the caller unfollowed, and does not call the subscriber port.
    /// </summary>
    public bool AutoFollowOnScan { get; set; }
}
