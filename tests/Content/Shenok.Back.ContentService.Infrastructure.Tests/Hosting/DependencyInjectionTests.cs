using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.ContentService.Infrastructure.Messaging;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Hosting;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Infrastructure_requires_the_content_and_blob_connection_strings()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var missingContent = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddContentInfrastructure(empty));
        Assert.Contains("ConnectionStrings:content", missingContent.Message, StringComparison.Ordinal);

        var contentOnly = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content"
        }).Build();
        var missingBlobs = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddContentInfrastructure(contentOnly));
        Assert.Contains("ConnectionStrings:blobs", missingBlobs.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_api_project()
    {
        var names = typeof(ContentDbContext).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("Shenok.Back.ContentService.Application", names);
        Assert.Contains("Shenok.Back.ContentService.Domain", names);
        Assert.DoesNotContain(names, name => name is not null && name.Contains("ContentService.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name is not null && name.Contains("EngagementService", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_container_name_fails_host_startup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            ["BlobStorage:ContainerName"] = "Content",
            ["Packaging:Enabled"] = "false"
        });
        builder.Services.AddContentInfrastructure(builder.Configuration);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var exception = await Record.ExceptionAsync(() => host.StartAsync(timeout.Token));

        Assert.NotNull(exception);
        Assert.Contains("ContainerName", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disabled_packaging_starts_without_opening_postgres_or_blob_storage()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            ["Packaging:Enabled"] = "false"
        });
        builder.Services.AddContentInfrastructure(builder.Configuration);
        using var host = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await host.StartAsync(timeout.Token);
        await host.StopAsync(timeout.Token);
    }

    [Fact]
    public void Disabled_messaging_does_not_register_the_outbox_publisher()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            ["Packaging:Enabled"] = "false",
            ["Messaging:Enabled"] = "false"
        }).Build();
        var services = new ServiceCollection();

        services.AddContentInfrastructure(configuration);

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(OutboxPublisher));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IWaveformEventPublisher));
    }

    [Fact]
    public void Enabled_messaging_requires_a_rabbit_connection_string()
    {
        var missing = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            ["Packaging:Enabled"] = "false",
            ["Messaging:Enabled"] = "true"
        }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddContentInfrastructure(missing));
        Assert.Contains("ConnectionStrings:rabbitmq", exception.Message, StringComparison.Ordinal);

        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = "Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content",
            ["ConnectionStrings:blobs"] = "UseDevelopmentStorage=true",
            ["ConnectionStrings:rabbitmq"] = "amqp://guest:guest@127.0.0.1:5672",
            ["Packaging:Enabled"] = "false",
            ["Messaging:Enabled"] = "true",
            ["Messaging:PollIntervalSeconds"] = "1",
            ["Messaging:BatchSize"] = "20"
        }).Build();
        var services = new ServiceCollection();
        services.AddContentInfrastructure(configured);

        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(OutboxPublisher));
    }
}
