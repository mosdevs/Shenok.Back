namespace Shenok.Back.ContentService.Application.Counters;

/// <summary>
/// Phase 2 accepts the counter command shape and does not change <c>PageCounter</c> or <c>VoiceCounter</c>.
/// Phase 6 replaces the throw with one guarded SQL update.
/// </summary>
public sealed class CounterUpdateDeferredException : Exception
{
    public CounterUpdateDeferredException()
        : base("Content counters are applied in phase 6 and were not changed.")
    {
    }
}
