using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Domain.Aggregates.Counters;

/// <summary>
/// Projection row keyed by voice. PlayCount starts at zero.
/// The counter ledger updates it with guarded SQL. Do not increment it from <see cref="Voice"/>.
/// </summary>
public sealed class VoiceCounter
{
    public VoiceId VoiceId { get; private set; } = null!;
    public long PlayCount { get; private set; }

    private VoiceCounter()
    {
    }

    private VoiceCounter(VoiceId voiceId) => VoiceId = voiceId;

    public static VoiceCounter Create(VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(voiceId);
        return new VoiceCounter(voiceId);
    }
}
