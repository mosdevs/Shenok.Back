using Shenok.Back.Application.Abstractions.Exceptions;

namespace Shenok.Back.ContentService.Application.Common;

internal static class IdempotencyKey
{
    public const int MaxLength = 128;

    public static void Require(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength || value.Trim().Length != value.Length)
            throw new ConflictException("Idempotency key is invalid.");

        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not (':' or '-' or '_'))
                throw new ConflictException("Idempotency key is invalid.");
        }
    }
}
