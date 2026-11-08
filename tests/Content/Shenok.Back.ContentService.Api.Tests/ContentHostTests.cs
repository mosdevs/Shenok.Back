using Microsoft.Extensions.Configuration;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.ContentService.Api.Tests;

public sealed class ContentHostTests
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

            var exception = Assert.Throws<InvalidOperationException>(() => ContentHost.RequireApiKey(configuration));
            Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        }
    }

    [Fact]
    public void Migration_runs_unless_startup_turns_it_off()
    {
        var missing = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ContentHost.MigrateOnStartupKey] = "true"
        }).Build();
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ContentHost.MigrateOnStartupKey] = "false"
        }).Build();

        Assert.True(ContentHost.ShouldMigrate(missing));
        Assert.True(ContentHost.ShouldMigrate(enabled));
        Assert.False(ContentHost.ShouldMigrate(disabled));
    }
}
