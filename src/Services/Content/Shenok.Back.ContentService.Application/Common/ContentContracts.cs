namespace Shenok.Back.ContentService.Application.Common;

public sealed record VoiceSummary(Guid VoiceId, string Title, double? DurationSeconds, string PackagingStatus);

public sealed record PageResponse(
    Guid PageId,
    Guid OwnerId,
    string Title,
    string Description,
    string? CoverImageUrl,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<VoiceSummary> Voices);

public sealed record VoiceResponse(
    Guid VoiceId,
    Guid PageId,
    string Title,
    double? DurationSeconds,
    string PackagingStatus);
