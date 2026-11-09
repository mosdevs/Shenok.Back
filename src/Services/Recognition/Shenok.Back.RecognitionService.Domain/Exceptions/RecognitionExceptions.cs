using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Exceptions;

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
        : base("Layout version must be at least 1.")
    {
        Value = value;
    }
}

public sealed class InvalidWaveformTargetException : DomainException
{
    public WaveformTargetType TargetType { get; }

    public Guid TargetId { get; }

    public InvalidWaveformTargetException(WaveformTargetType targetType, Guid targetId)
        : base("Waveform target must be Page or Voice, and the target id cannot be empty.")
    {
        TargetType = targetType;
        TargetId = targetId;
    }
}
