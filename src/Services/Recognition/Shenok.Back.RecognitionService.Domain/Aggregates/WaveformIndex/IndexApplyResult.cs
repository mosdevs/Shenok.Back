namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// What happened when a fact was applied to an existing index row.
/// These are not failures. The caller still records the event id so the bus does not retry it.
/// </summary>
public readonly record struct IndexApplyResult
{
    private IndexApplyResult(IndexApplyOutcome outcome) => Outcome = outcome;

    public IndexApplyOutcome Outcome { get; }

    public static IndexApplyResult Applied { get; } = new(IndexApplyOutcome.Applied);

    public static IndexApplyResult Unchanged { get; } = new(IndexApplyOutcome.Unchanged);

    public static IndexApplyResult IgnoredStale { get; } = new(IndexApplyOutcome.IgnoredStale);

    public static IndexApplyResult IgnoredConflict { get; } = new(IndexApplyOutcome.IgnoredConflict);

    public override string ToString() => Outcome.ToString();
}

/// <summary>
/// Discriminator for <see cref="IndexApplyResult"/>. The row is written only for <see cref="Applied"/>.
/// Zero is unused so <c>default(IndexApplyResult)</c> is not <see cref="Applied"/>.
/// </summary>
public enum IndexApplyOutcome
{
    Applied = 1,
    Unchanged = 2,
    IgnoredStale = 3,
    IgnoredConflict = 4
}
