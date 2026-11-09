namespace Shenok.Back.ContentService.Application.Counters;

/// <summary>
/// Kept so an internal counter route can still answer 501 if a caller defers the update.
/// The counter use cases apply the count and do not throw this.
/// </summary>
public sealed class CounterUpdateDeferredException : Exception
{
    public CounterUpdateDeferredException()
        : base("Content counters are applied in phase 6 and were not changed.")
    {
    }
}
