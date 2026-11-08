using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;

/// <summary>
/// Identity of one user's listening record for one voice. Equality is the pair.
/// </summary>
public sealed record ListeningRecordId
{
    public UserId UserId { get; }
    public VoiceId VoiceId { get; }

    public ListeningRecordId(UserId userId, VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        UserId = userId;
        VoiceId = voiceId;
    }

    public override string ToString() => $"{UserId}/{VoiceId}";
}
