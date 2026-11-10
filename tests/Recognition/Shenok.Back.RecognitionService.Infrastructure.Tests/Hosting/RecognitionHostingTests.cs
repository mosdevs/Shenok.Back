using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shenok.Back.RecognitionService.Application;
using Shenok.Back.RecognitionService.Infrastructure.Messaging;
using InfrastructureDependencyInjection = Shenok.Back.RecognitionService.Infrastructure.DependencyInjection;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests.Hosting;

/// <summary>
/// Locks the hosting keys the AppHost will inject. No Postgres and no RabbitMQ process.
/// </summary>
public sealed class RecognitionHostingTests
{
    private const string RecognitionConnection =
        "Host=127.0.0.1;Port=1;Database=recognition;Username=recognition;Password=recognition";

    private const string UnreachableRabbit = "amqp://guest:guest@127.0.0.1:1";

    [Fact]
    public void Infrastructure_requires_the_recognition_connection_string()
    {
        foreach (var connection in new string?[] { null, "", "   " })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{InfrastructureDependencyInjection.ConnectionStringName}"] = connection
            }).Build();
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddRecognitionInfrastructure(configuration));

            Assert.Equal(
                $"ConnectionStrings:{InfrastructureDependencyInjection.ConnectionStringName} is required.",
                exception.Message);
            Assert.Empty(services);
        }
    }

    [Fact]
    public void Messaging_is_off_unless_the_flag_is_true()
    {
        Assert.False(InfrastructureDependencyInjection.IsMessagingEnabled(new ConfigurationBuilder().Build()));
        Assert.False(InfrastructureDependencyInjection.IsMessagingEnabled(Configuration(messaging: false)));
        Assert.True(InfrastructureDependencyInjection.IsMessagingEnabled(Configuration(messaging: true)));
    }

    [Fact]
    public void Disabled_messaging_does_not_register_the_inbox_consumer()
    {
        var services = new ServiceCollection();

        services.AddRecognitionInfrastructure(Configuration(messaging: false));

        Assert.DoesNotContain(services, IsInboxConsumer);
    }

    [Fact]
    public void Enabled_messaging_requires_a_rabbit_connection_string()
    {
        foreach (var rabbit in new string?[] { null, "", "   " })
        {
            var services = new ServiceCollection();
            var configuration = Configuration(messaging: true, rabbit: rabbit, includeRabbit: true);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.AddRecognitionInfrastructure(configuration));

            Assert.Equal(
                $"ConnectionStrings:{InfrastructureDependencyInjection.RabbitConnectionStringName} is required.",
                exception.Message);
            Assert.DoesNotContain(services, IsInboxConsumer);
        }
    }

    [Fact]
    public void Enabled_messaging_registers_the_broker_on_the_ready_tag()
    {
        var services = new ServiceCollection();

        services.AddRecognitionInfrastructure(Configuration(messaging: true, rabbit: UnreachableRabbit, includeRabbit: true));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(WaveformInboxConsumer));
        using var provider = services.BuildServiceProvider();
        var ready = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations
            .Where(registration => registration.Tags.Contains(InfrastructureDependencyInjection.ReadyTag))
            .ToArray();

        var broker = Assert.Single(ready);
        Assert.Equal(InfrastructureDependencyInjection.BrokerHealthCheckName, broker.Name);
    }

    [Fact(Timeout = 20_000)]
    public async Task Disabled_messaging_starts_without_a_broker()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(Settings(messaging: false));
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRecognitionApplication();
        builder.Services.AddRecognitionInfrastructure(builder.Configuration);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await host.StartAsync(timeout.Token);
        try
        {
            Assert.Null(host.Services.GetService<WaveformInboxConsumer>());
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }

    [Fact(Timeout = 20_000)]
    public async Task A_down_broker_leaves_the_host_up_and_the_ready_check_unhealthy()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(Settings(messaging: true, rabbit: UnreachableRabbit, includeRabbit: true));
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRecognitionApplication();
        builder.Services.AddRecognitionInfrastructure(builder.Configuration);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await host.StartAsync(timeout.Token);
        try
        {
            var consumer = host.Services.GetRequiredService<WaveformInboxConsumer>();
            Assert.False(consumer.IsConnected);

            var report = await host.Services.GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(timeout.Token);

            Assert.Equal(HealthStatus.Unhealthy, report.Status);
            Assert.True(report.Entries.TryGetValue(InfrastructureDependencyInjection.BrokerHealthCheckName, out var entry));
            Assert.Equal(HealthStatus.Unhealthy, entry.Status);
            Assert.Equal("The waveform broker is not connected.", entry.Description);
        }
        finally
        {
            await host.StopAsync(timeout.Token);
        }
    }

    private static bool IsInboxConsumer(ServiceDescriptor descriptor)
        => descriptor.ServiceType == typeof(WaveformInboxConsumer)
           || descriptor.ImplementationType == typeof(WaveformInboxConsumer);

    private static IConfiguration Configuration(bool messaging, string? rabbit = null, bool includeRabbit = false)
        => new ConfigurationBuilder().AddInMemoryCollection(Settings(messaging, rabbit, includeRabbit)).Build();

    private static Dictionary<string, string?> Settings(bool messaging, string? rabbit = null, bool includeRabbit = false)
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{InfrastructureDependencyInjection.ConnectionStringName}"] = RecognitionConnection,
            [$"{InfrastructureDependencyInjection.MessagingSection}:Enabled"] = messaging ? "true" : "false"
        };
        if (includeRabbit)
            values[$"ConnectionStrings:{InfrastructureDependencyInjection.RabbitConnectionStringName}"] = rabbit;

        return values;
    }
}
