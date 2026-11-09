namespace Shenok.Back.Application.Abstractions.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>
/// The work was valid and could not be saved after the allowed retries.
/// Mapped to HTTP 503. The message is a stable client detail, not an internal exception.
/// </summary>
public sealed class UnavailableException : Exception
{
    public UnavailableException(string message) : base(message) { }

    public UnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
