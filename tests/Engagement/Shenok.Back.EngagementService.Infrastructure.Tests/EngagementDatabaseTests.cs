using Microsoft.Extensions.Configuration;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

public sealed class EngagementDatabaseTests
{
    [Fact]
    public void MigrateOnStartup_defaults_to_true()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.True(EngagementDatabase.ShouldMigrate(configuration));
    }

    [Fact]
    public void MigrateOnStartup_false_is_off()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EngagementDatabase.MigrateOnStartupKey] = "false"
            })
            .Build();

        Assert.False(EngagementDatabase.ShouldMigrate(configuration));
    }
}
