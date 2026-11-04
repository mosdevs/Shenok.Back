using System.Security.Cryptography;
using System.Text;

namespace Shenok.Back.Application.Abstractions.Security;

public static class Sha256Hasher
{
    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
