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
    public void Content_counters_call_content_after_the_save()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] =
                    "Host=127.0.0.1;Port=1;Database=engagement;Username=engagement;Password=engagement",
                [$"{ContentCounterOptions.SectionName}:BaseUrl"] = "http://127.0.0.1:9",
                [ContentCounterOptions.ApiKeyConfigurationPath] = "engagement-test-key"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEngagementInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var counters = scope.ServiceProvider.GetRequiredService<IContentCounters>();
        Assert.IsType<HttpContentCounters>(counters);
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
