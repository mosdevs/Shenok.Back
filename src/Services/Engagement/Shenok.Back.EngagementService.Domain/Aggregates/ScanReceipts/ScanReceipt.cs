using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;

/// <summary>
/// Frozen result of one client scan. A later follow or seek must not change what a retried scan id returns.
/// There is no mutator: a duplicate id is rejected by the database, then the stored row is reloaded.
/// </summary>
public sealed class ScanReceipt : AggregateRoot<ScanId>
{
    public UserId UserId { get; private set; } = null!;
    public WaveformId WaveformId { get; private set; } = null!;
    public VoiceId VoiceId { get; private set; } = null!;
    public PageId PageId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool Unlocked { get; private set; }
    public bool IsFollowing { get; private set; }
    public bool PromptFollow { get; private set; }
    public PlaybackPosition ResumePosition { get; private set; } = null!;

    private ScanReceipt()
    {
    }

    private ScanReceipt(
        ScanId scanId,
        UserId userId,
        WaveformId waveformId,
        VoiceId voiceId,
        PageId pageId,
        bool unlocked,
        bool isFollowing,
        bool promptFollow,
        PlaybackPosition resumePosition,
        DateTime createdAt) : base(scanId)
    {
        UserId = userId;
        WaveformId = waveformId;
        VoiceId = voiceId;
        PageId = pageId;
        CreatedAt = createdAt;
        Unlocked = unlocked;
        IsFollowing = isFollowing;
        PromptFollow = promptFollow;
        ResumePosition = resumePosition;
    }

    public static ScanReceipt Record(
        ScanId scanId,
        UserId userId,
        WaveformId waveformId,
        VoiceId voiceId,
        PageId pageId,
        bool unlocked,
        bool isFollowing,
        bool promptFollow,
        PlaybackPosition resumePosition)
    {
        ArgumentNullException.ThrowIfNull(scanId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(waveformId);
        ArgumentNullException.ThrowIfNull(voiceId);
        ArgumentNullException.ThrowIfNull(pageId);
        ArgumentNullException.ThrowIfNull(resumePosition);

        return new ScanReceipt(
            scanId,
            userId,
            waveformId,
            voiceId,
            pageId,
            unlocked,
            isFollowing,
            promptFollow,
            resumePosition,
            DateTime.UtcNow);
    }
}
