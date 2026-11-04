using System.Security.Cryptography;

namespace Shenok.Back.IdentityService.Application.Common;

public static class OtpCodeFactory
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt32(bytes) % 1_000_000;
        return value.ToString("D6");
    }
}
