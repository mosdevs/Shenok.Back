namespace Shenok.Back.ContentService.Application.Common;

public sealed record VoiceSummary(
    Guid VoiceId,
    string Title,
    double? DurationSeconds,
    string PackagingStatus,
    long PlayCount);

public sealed record PageResponse(
    Guid PageId,
    Guid OwnerId,
    string Title,
    string Description,
    string? CoverImageUrl,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<VoiceSummary> Voices,
    long SubscriberCount);

public sealed record VoiceResponse(
    Guid VoiceId,
    Guid PageId,
    string Title,
    double? DurationSeconds,
    string PackagingStatus);
