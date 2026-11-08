using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Infrastructure;
using Shenok.Back.EngagementService.Infrastructure.Counters;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Infrastructure_requires_the_engagement_connection_string()
    {
        var configuration = new ConfigurationBuilder().Build();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddEngagementInfrastructure(configuration));

        Assert.Contains("ConnectionStrings:engagement", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEngagementInfrastructure_rejects_null_arguments()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddEngagementInfrastructure(configuration));
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddEngagementInfrastructure(null!));
    }

    [Fact]
    public async Task Content_counters_are_the_no_op_until_phase_6()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] =
                    "Host=127.0.0.1;Port=1;Database=engagement;Username=engagement;Password=engagement"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEngagementInfrastructure(configuration);
        await using var provider = services.BuildServiceProvider();

        var counters = provider.GetRequiredService<IContentCounters>();
        Assert.IsType<NoOpContentCounters>(counters);
        await counters.ApplySubscriberDeltaAsync(Guid.NewGuid(), Guid.NewGuid(), delta: 1);
        await counters.ApplyPlayAsync("play-1", Guid.NewGuid());
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_api_project()
    {
        var names = typeof(EngagementDbContext).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("Shenok.Back.EngagementService.Application", names);
        Assert.Contains("Shenok.Back.EngagementService.Domain", names);
        Assert.DoesNotContain(names, name => name is not null && name.Contains("EngagementService.Api", StringComparison.Ordinal));
    }
}
