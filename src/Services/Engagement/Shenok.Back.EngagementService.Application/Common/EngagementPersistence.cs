namespace Shenok.Back.EngagementService.Application.Common;

/// <summary>
/// How many times a use case may save after a lost insert or an xmin conflict.
/// The second attempt reloads the winning row and applies the same domain method again.
/// A second retryable failure throws UnavailableException (HTTP 503).
/// A lost <see cref="EngagementInsertTarget.ScanReceipt"/> or <see cref="EngagementInsertTarget.PlaySession"/>
/// insert is reloaded even on the last attempt, because that row may belong to the caller.
/// </summary>
internal static class EngagementPersistence
{
    public const int MaxAttempts = 2;
}

/// <summary>
/// Which primary key rejected an insert. Infrastructure sets this from the Postgres constraint name.
/// </summary>
public enum EngagementInsertTarget
{
    ScanReceipt,
    UserPageRelationship,
    ListeningRecord,
    PlaySession
}

/// <summary>
/// An insert lost its primary key to a concurrent request.
/// This is not an HTTP status. The use case reloads the winner.
/// </summary>
/// <remarks>
/// Infrastructure contract: before throwing, detach every <c>Added</c> entry from the failed transaction
/// so the same context can add the next row. The next repository get must return the committed winner,
/// not the instance this attempt mutated. Do not translate this into <c>ConflictException</c>.
/// A scan id or play session id that belongs to someone else becomes a conflict only after
/// <c>BelongsTo</c> returns false.
/// </remarks>
public sealed class EngagementInsertConflictException : Exception
{
    public EngagementInsertConflictException(EngagementInsertTarget target)
        : base("The insert lost a unique-key race.")
    {
        Target = target;
    }

    public EngagementInsertTarget Target { get; }
}

/// <summary>
/// The xmin token on a relationship, listening record, or play session rejected the update.
/// </summary>
/// <remarks>
/// Infrastructure contract: the transaction is aborted. Detach every <c>Added</c> entry before throwing,
/// and detach the conflicted row so the next get reads the committed values. The use case mutates the
/// instance it loaded; returning that same instance makes the retry a no-op.
/// <c>Unlock</c>, <c>Follow</c>, <c>Unfollow</c>, <c>ReportProgress</c>, and <c>AcceptFirstReport</c>
/// are safe to call on the reloaded row.
/// </remarks>
public sealed class EngagementConcurrencyException : Exception
{
    public EngagementConcurrencyException()
        : base("The row was updated by another request.")
    {
    }
}

/// <summary>
/// Conflict messages returned to the caller. They name the clash and carry none of the stored snapshot.
/// </summary>
public static class EngagementConflict
{
    public const string ScanBelongsToAnotherUser =
        "This scan id is already stored for a different user.";

    public const string PlaySessionBelongsToAnotherListener =
        "This play session belongs to a different user or voice.";
}
