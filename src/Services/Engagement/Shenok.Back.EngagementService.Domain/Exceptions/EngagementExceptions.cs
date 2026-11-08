using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.EngagementService.Domain.Exceptions;

public sealed class InvalidScanIdException : DomainException
{
    public InvalidScanIdException(string message) : base(message) { }
}

public sealed class InvalidPlaySessionIdException : DomainException
{
    public InvalidPlaySessionIdException(string message) : base(message) { }
}

public sealed class NegativePlaybackPositionException : DomainException
{
    public int Value { get; }

    public NegativePlaybackPositionException(int value)
        : base("Playback position cannot be negative.")
    {
        Value = value;
    }
}
