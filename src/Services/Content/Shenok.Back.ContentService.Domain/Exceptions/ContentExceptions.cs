using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.ContentService.Domain.Exceptions;

public sealed class InvalidPageTitleException : DomainException
{
    public InvalidPageTitleException(string message) : base(message) { }
}

public sealed class InvalidPageDescriptionException : DomainException
{
    public InvalidPageDescriptionException(string message) : base(message) { }
}

public sealed class InvalidCoverImageUrlException : DomainException
{
    public InvalidCoverImageUrlException(string message) : base(message) { }
}

public sealed class InvalidVoiceTitleException : DomainException
{
    public InvalidVoiceTitleException(string message) : base(message) { }
}

public sealed class NotPageOwnerException : DomainException
{
    public PageId PageId { get; }
    public OwnerId ActorId { get; }

    public NotPageOwnerException(PageId pageId, OwnerId actorId)
        : base($"Caller {actorId} does not own page {pageId}.")
    {
        PageId = pageId;
        ActorId = actorId;
    }
}

public sealed class PageArchivedException : DomainException
{
    public PageId PageId { get; }

    public PageArchivedException(PageId pageId) : base($"Page {pageId} is archived.")
    {
        PageId = pageId;
    }
}

public sealed class PageAlreadyPublishedException : DomainException
{
    public PageId PageId { get; }

    public PageAlreadyPublishedException(PageId pageId) : base($"Page {pageId} is already published.")
    {
        PageId = pageId;
    }
}

public sealed class CannotPublishArchivedPageException : DomainException
{
    public PageId PageId { get; }

    public CannotPublishArchivedPageException(PageId pageId) : base($"Archived page {pageId} cannot be published.")
    {
        PageId = pageId;
    }
}

public sealed class PageHasNoVoicesException : DomainException
{
    public PageId PageId { get; }

    public PageHasNoVoicesException(PageId pageId) : base($"Page {pageId} has no voices.")
    {
        PageId = pageId;
    }
}

public sealed class VoicesNotInEpisodeOrderException : DomainException
{
    public PageId PageId { get; }

    public VoicesNotInEpisodeOrderException(PageId pageId)
        : base($"Page {pageId} has voices that are not in the episode order.")
    {
        PageId = pageId;
    }
}

public sealed class VoiceNotReadyForPublishException : DomainException
{
    public VoiceId VoiceId { get; }
    public PackagingStatus? PackagingStatus { get; }

    public VoiceNotReadyForPublishException(VoiceId voiceId, PackagingStatus? packagingStatus)
        : base(packagingStatus is null
            ? $"Voice {voiceId} has no packaging status and cannot be published."
            : $"Voice {voiceId} is {packagingStatus} and cannot be published.")
    {
        VoiceId = voiceId;
        PackagingStatus = packagingStatus;
    }
}

public sealed class DuplicateVoiceOnPageException : DomainException
{
    public PageId PageId { get; }
    public VoiceId VoiceId { get; }

    public DuplicateVoiceOnPageException(PageId pageId, VoiceId voiceId)
        : base($"Voice {voiceId} is already on page {pageId}.")
    {
        PageId = pageId;
        VoiceId = voiceId;
    }
}

public sealed class InvalidVoiceOrderException : DomainException
{
    public InvalidVoiceOrderException()
        : base("Voice order must list each voice on the page exactly once.")
    {
    }
}

public sealed class InvalidAudioAssetException : DomainException
{
    public InvalidAudioAssetException(string message) : base(message) { }
}

public sealed class InvalidPackagingTransitionException : DomainException
{
    public PackagingStatus From { get; }
    public PackagingStatus To { get; }

    public InvalidPackagingTransitionException(PackagingStatus from, PackagingStatus to)
        : base($"Cannot change packaging status from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}

public sealed class InvalidDurationException : DomainException
{
    public InvalidDurationException() : base("Duration must be a finite number of seconds greater than zero.") { }
}

public sealed class AudioNotReadyException : DomainException
{
    public VoiceId VoiceId { get; }
    public PackagingStatus PackagingStatus { get; }

    public AudioNotReadyException(VoiceId voiceId, PackagingStatus packagingStatus)
        : base($"Voice {voiceId} cannot be played while packaging is {packagingStatus}.")
    {
        VoiceId = voiceId;
        PackagingStatus = packagingStatus;
    }
}

public sealed class InvalidWaveformCodeException : DomainException
{
    public long Value { get; }

    public InvalidWaveformCodeException(long value)
        : base($"Waveform code must be a positive integer of at most {WaveformCode.MaxBits} bits.")
    {
        Value = value;
    }
}

public sealed class InvalidLayoutVersionException : DomainException
{
    public int Value { get; }

    public InvalidLayoutVersionException(int value)
        : base($"Layout version {value} is not supported.")
    {
        Value = value;
    }
}

public sealed class CannotRegisterWaveformOnArchivedPageException : DomainException
{
    public WaveformTargetType TargetType { get; }
    public Guid TargetId { get; }

    public CannotRegisterWaveformOnArchivedPageException(WaveformTargetType targetType, Guid targetId)
        : base("Cannot register a waveform because the page is archived.")
    {
        TargetType = targetType;
        TargetId = targetId;
    }
}

public sealed class WaveformAlreadyRevokedException : DomainException
{
    public WaveformId WaveformId { get; }

    public WaveformAlreadyRevokedException(WaveformId waveformId)
        : base($"Waveform {waveformId} is already revoked.")
    {
        WaveformId = waveformId;
    }
}

public sealed class WaveformNotActiveException : DomainException
{
    public WaveformId WaveformId { get; }

    public WaveformNotActiveException(WaveformId waveformId)
        : base($"Waveform {waveformId} is not active.")
    {
        WaveformId = waveformId;
    }
}
