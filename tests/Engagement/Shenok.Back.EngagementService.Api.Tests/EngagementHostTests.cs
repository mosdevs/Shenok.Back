using Microsoft.Extensions.Configuration;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.EngagementService.Api.Tests;

public sealed class EngagementHostTests
{
    [Fact]
    public void A_blank_api_key_is_rejected_before_the_host_listens()
    {
        foreach (var key in new string?[] { null, "", "   " })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalCaller.ApiKeyConfigurationPath] = key
            }).Build();

            var exception = Assert.Throws<InvalidOperationException>(() => EngagementHost.RequireApiKey(configuration));
            Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        }
    }

    [Fact]
    public void RequireApiKey_rejects_a_null_configuration()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => EngagementHost.RequireApiKey(null!));
        Assert.Equal("configuration", exception.ParamName);
    }
}
