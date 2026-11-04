using Shenok.Back.IdentityService.Infrastructure.Messaging;

namespace Shenok.Back.IdentityService.IntegrationTests;

public class KavenegarReceptorTests
{
    [Theory]
    [InlineData("+989121234567", "09121234567")]
    [InlineData("+974211234565", "00974211234565")]
    public void FromE164_matches_lookup_receptor_format(string e164, string receptor) =>
        Assert.Equal(receptor, KavenegarReceptor.FromE164(e164));
}
