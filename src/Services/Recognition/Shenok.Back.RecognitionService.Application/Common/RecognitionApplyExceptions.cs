namespace Shenok.Back.RecognitionService.Application.Common;

/// <summary>
/// The inbox primary key already holds this event id. Another consumer committed it.
/// </summary>
/// <remarks>
/// Infrastructure contract: a unique violation on <c>pk_inbox_messages</c> (Postgres <c>23505</c>)
/// detaches every Added and Modified entry, then throws this. The apply use case returns. It does not retry
/// and it does not turn this into an HTTP result. The consumer acknowledges the delivery.
/// </remarks>
public sealed class InboxAlreadyProcessedException : Exception
{
    public InboxAlreadyProcessedException()
        : this(null)
    {
    }

    public InboxAlreadyProcessedException(Exception? innerException)
        : base("The waveform event was already recorded.", innerException)
    {
    }
}

/// <summary>
/// The waveform index row lost an xmin race.
/// </summary>
/// <remarks>
/// Infrastructure contract: detach every Added entry and the conflicted row before throwing,
/// so the next <c>GetAsync</c> returns the committed row and not the instance this attempt mutated.
/// The apply use case reloads and tries again, at most three times, then lets this propagate.
/// The consumer nacks and requeues. This is not an HTTP result.
/// </remarks>
public sealed class IndexConcurrencyException : Exception
{
    public IndexConcurrencyException()
        : this(null)
    {
    }

    public IndexConcurrencyException(Exception? innerException)
        : base("The waveform index row was updated by another writer.", innerException)
    {
    }
}
