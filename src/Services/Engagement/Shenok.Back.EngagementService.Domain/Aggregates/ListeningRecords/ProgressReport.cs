namespace Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;

/// <summary>
/// Result of one progress report. <see cref="NewlyCompleted"/> is true only on the false-to-true transition.
/// </summary>
public readonly record struct ProgressReport(bool Accepted, bool NewlyCompleted);
