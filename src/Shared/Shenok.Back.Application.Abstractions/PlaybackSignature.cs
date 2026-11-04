using System.Security.Cryptography;
using System.Text;

namespace Shenok.Back.Application.Abstractions;

public static class PlaybackSignature
{
    public static string Sign(string signingKey, string resource, long expiresUnixSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        var payload = Encoding.UTF8.GetBytes($"{resource}:{expiresUnixSeconds}");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Matches(string signingKey, string resource, long expiresUnixSeconds, string signature)
    {
        var expected = Sign(signingKey, resource, expiresUnixSeconds);
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(signature);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
