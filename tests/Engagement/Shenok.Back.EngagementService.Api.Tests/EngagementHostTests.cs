using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api;
using Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

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

    [Fact]
    public void AddServices_rejects_a_blank_api_key_before_registering_infrastructure()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EngagementHost.AddServices(services, configuration, includeInfrastructure: true));

        Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void Startup_migration_stays_on_unless_a_replica_turns_it_off()
    {
        var missing = new ConfigurationBuilder().Build();
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [EngagementDatabase.MigrateOnStartupKey] = "false"
        }).Build();

        Assert.True(EngagementDatabase.ShouldMigrate(missing));
        Assert.False(EngagementDatabase.ShouldMigrate(disabled));
    }

    [Fact]
    public void Production_registration_checks_the_database_on_ready_and_registers_no_actor_system()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = "engagement-test-key",
            ["ConnectionStrings:engagement"] = "Host=localhost;Port=5432;Database=engagement;Username=postgres;Password=postgres",
            [EngagementDatabase.MigrateOnStartupKey] = "false"
        }).Build();

        EngagementHost.AddServices(services, configuration, includeInfrastructure: true);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.Name.Contains("EngagementDbContext", StringComparison.Ordinal)
            || (descriptor.ImplementationType?.Name.Contains("EngagementDbContext", StringComparison.Ordinal) ?? false));

        using var provider = services.BuildServiceProvider();
        var ready = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Where(registration => registration.Tags.Contains(EngagementHost.ReadyTag))
            .ToArray();
        Assert.Single(ready);
        Assert.Contains("EngagementDbContext", ready[0].Name, StringComparison.Ordinal);

        Assert.DoesNotContain(services, descriptor =>
        {
            var names = new[]
            {
                descriptor.ServiceType.FullName,
                descriptor.ImplementationType?.FullName,
                descriptor.ImplementationInstance?.GetType().FullName
            };
            return names.Any(name => name is not null && (
                name.Contains("Akka.Actor.ActorSystem", StringComparison.Ordinal)
                || name.Contains("AkkaHostedService", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void Template_types_are_not_compiled_into_engagement()
    {
        Assert.DoesNotContain(typeof(ScanId).Assembly.GetTypes(), type => type.Name == "Class1");
        Assert.DoesNotContain(typeof(FollowPageRequest).Assembly.GetTypes(), type => type.Name == "Class1");
        Assert.DoesNotContain(typeof(EngagementDbContext).Assembly.GetTypes(), type => type.Name == "Class1");
    }
}
