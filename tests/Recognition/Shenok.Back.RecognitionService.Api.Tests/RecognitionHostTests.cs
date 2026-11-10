using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.RecognitionService.Api.Controllers;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;
using Shenok.Back.RecognitionService.Infrastructure;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;
using WaveformIdentity = Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex.WaveformId;

namespace Shenok.Back.RecognitionService.Api.Tests;

public sealed class RecognitionHostTests
{
    private static readonly Guid StoredWaveformId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid StoredTargetId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void A_blank_api_key_is_rejected_before_the_host_listens()
    {
        foreach (var key in new string?[] { null, "", "   " })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [InternalCaller.ApiKeyConfigurationPath] = key
            }).Build();

            var exception = Assert.Throws<InvalidOperationException>(() => RecognitionHost.RequireApiKey(configuration));
            Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        }
    }

    [Fact]
    public void RequireApiKey_rejects_a_null_configuration()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => RecognitionHost.RequireApiKey(null!));
        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public void AddServices_rejects_a_blank_api_key_before_registering_infrastructure()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecognitionHost.AddServices(services, configuration, includeInfrastructure: true));

        Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void Startup_migration_stays_on_unless_a_replica_turns_it_off()
    {
        var missing = new ConfigurationBuilder().Build();
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RecognitionDatabase.MigrateOnStartupKey] = "false"
        }).Build();

        Assert.True(RecognitionDatabase.ShouldMigrate(missing));
        Assert.False(RecognitionDatabase.ShouldMigrate(disabled));
    }

    [Fact]
    public void Ready_is_the_database_until_messaging_is_enabled()
    {
        var quietServices = new ServiceCollection();
        RecognitionHost.AddServices(quietServices, HostConfiguration(messaging: false, includeRabbit: false), includeInfrastructure: true);
        Assert.DoesNotContain(quietServices, IsInboxConsumer);
        using (var quietProvider = quietServices.BuildServiceProvider())
        {
            var quiet = ReadyRegistrations(quietProvider);
            var database = Assert.Single(quiet);
            Assert.Contains("RecognitionDbContext", database.Name, StringComparison.Ordinal);
            Assert.DoesNotContain(quiet, registration => registration.Name == DependencyInjection.BrokerHealthCheckName);
        }

        var missingRabbit = Assert.Throws<InvalidOperationException>(() =>
            RecognitionHost.AddServices(
                new ServiceCollection(),
                HostConfiguration(messaging: true, includeRabbit: false),
                includeInfrastructure: true));
        Assert.Equal(
            $"ConnectionStrings:{DependencyInjection.RabbitConnectionStringName} is required.",
            missingRabbit.Message);

        var liveServices = new ServiceCollection();
        RecognitionHost.AddServices(liveServices, HostConfiguration(messaging: true, includeRabbit: true), includeInfrastructure: true);
        Assert.Contains(liveServices, IsInboxConsumer);
        using var liveProvider = liveServices.BuildServiceProvider();
        var ready = ReadyRegistrations(liveProvider);
        Assert.Equal(2, ready.Length);
        Assert.Contains(ready, registration => registration.Name.Contains("RecognitionDbContext", StringComparison.Ordinal));
        Assert.Contains(ready, registration => registration.Name == DependencyInjection.BrokerHealthCheckName);
        Assert.All(ready, registration => Assert.Contains(RecognitionHost.ReadyTag, registration.Tags));
    }

    [Fact]
    public void Template_types_and_native_image_libraries_are_absent()
    {
        var domain = typeof(WaveformCode).Assembly;
        var application = typeof(DecodeWaveformUseCase).Assembly;
        var infrastructure = typeof(RecognitionDbContext).Assembly;
        var api = typeof(DecodeController).Assembly;

        foreach (var assembly in new[] { domain, application, infrastructure, api })
        {
            Assert.DoesNotContain(assembly.GetTypes(), type => type.Name is "Class1" or "WeatherForecast");
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), IsNativeOrActorDependency);
        }

        Assert.Contains(infrastructure.GetReferencedAssemblies(), name => name.Name == "SixLabors.ImageSharp");
        Assert.Contains(infrastructure.GetReferencedAssemblies(), name => name.Name == "RabbitMQ.Client");
        Assert.DoesNotContain(domain.GetReferencedAssemblies(), name => name.Name is "SixLabors.ImageSharp" or "RabbitMQ.Client");
        Assert.DoesNotContain(application.GetReferencedAssemblies(), name => name.Name is "SixLabors.ImageSharp" or "RabbitMQ.Client");
        Assert.DoesNotContain(api.GetReferencedAssemblies(), name => name.Name is "SixLabors.ImageSharp" or "RabbitMQ.Client");
    }

    // The consumer is the production type, aimed at a closed broker port.
    // The index double keeps this test off Postgres. Persistence tests cover the stored row.
    [Fact(Timeout = 20_000)]
    public async Task Decode_answers_while_the_broker_is_down()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DecodeController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Configuration.AddInMemoryCollection(HostValues(messaging: true, includeRabbit: true));
        RecognitionHost.AddServices(builder.Services, builder.Configuration, includeInfrastructure: true);
        builder.Services.AddControllers().AddApplicationPart(typeof(DecodeController).Assembly);
        builder.Services.AddSingleton<IWaveformDecoder, ScriptedDecoder>();
        builder.Services.AddSingleton<IWaveformIndexRepository>(new MemoryIndex(StoredWaveformId, StoredTargetId));

        var app = builder.Build();
        var started = false;
        try
        {
            RecognitionHost.Map(app);
            await app.StartAsync();
            started = true;

            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var address = addresses?.Addresses.FirstOrDefault(item => item.StartsWith("http://", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The recognition host did not bind an HTTP address.");
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(address)
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/decode");
            request.Headers.TryAddWithoutValidation(InternalCaller.ApiKeyHeader, "recognition-test-key");
            var content = new ByteArrayContent([1]);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            request.Content = content;

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await ApiJson.ReadAsync(response);
            Assert.Equal(StoredWaveformId, body.GetProperty("waveformId").GetGuid());
            Assert.Equal("Page", body.GetProperty("targetType").GetString());
            Assert.Equal(StoredTargetId, body.GetProperty("targetId").GetGuid());
            Assert.False(body.TryGetProperty("waveformCode", out _));
        }
        finally
        {
            if (started)
                await app.StopAsync();

            await app.DisposeAsync();
        }
    }

    private static HealthCheckRegistration[] ReadyRegistrations(IServiceProvider provider)
        => provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Where(registration => registration.Tags.Contains(RecognitionHost.ReadyTag))
            .ToArray();

    private static bool IsInboxConsumer(ServiceDescriptor descriptor)
        => descriptor.ServiceType.Name == "WaveformInboxConsumer"
           || descriptor.ImplementationType?.Name == "WaveformInboxConsumer";

    private static bool IsNativeOrActorDependency(AssemblyName name)
        => name.Name is not null && (
            name.Name.StartsWith("Akka", StringComparison.Ordinal)
            || name.Name.StartsWith("Aspire", StringComparison.Ordinal)
            || name.Name.Contains("OpenCv", StringComparison.OrdinalIgnoreCase)
            || name.Name.Contains("FFmpeg", StringComparison.OrdinalIgnoreCase)
            || name.Name.Contains("Emgu", StringComparison.Ordinal));

    private static IConfiguration HostConfiguration(bool messaging, bool includeRabbit)
        => new ConfigurationBuilder().AddInMemoryCollection(HostValues(messaging, includeRabbit)).Build();

    private static Dictionary<string, string?> HostValues(bool messaging, bool includeRabbit)
    {
        var values = new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = "recognition-test-key",
            [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] =
                "Host=127.0.0.1;Port=1;Database=recognition;Username=recognition;Password=recognition",
            [RecognitionDatabase.MigrateOnStartupKey] = "false",
            [$"{DependencyInjection.MessagingSection}:Enabled"] = messaging ? "true" : "false"
        };
        if (includeRabbit)
        {
            values[$"ConnectionStrings:{DependencyInjection.RabbitConnectionStringName}"] =
                "amqp://guest:guest@127.0.0.1:1";
        }

        return values;
    }

    private sealed class ScriptedDecoder : IWaveformDecoder
    {
        public ValueTask<WaveformDecodeResult> DecodeAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
        {
            if (image.IsEmpty)
                return new ValueTask<WaveformDecodeResult>(new WaveformDecodeResult.Failure(WaveformDecodeResult.ImageUnreadable));

            return new ValueTask<WaveformDecodeResult>(new WaveformDecodeResult.Success(42));
        }
    }

    private sealed class MemoryIndex(Guid waveformId, Guid targetId) : IWaveformIndexRepository
    {
        private readonly WaveformIndexEntry _active = WaveformIndexEntry.Register(WaveformFact.Create(
            WaveformCode.From(42),
            WaveformIdentity.From(waveformId),
            WaveformTargetType.Page,
            targetId,
            LayoutVersion.Version1,
            new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid()));

        public Task<WaveformIndexEntry?> GetActiveAsync(WaveformCode code, CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not use the active-only read.");

        public Task<WaveformIndexEntry?> FindAsync(WaveformCode code, CancellationToken ct = default)
            => Task.FromResult(code == _active.Code ? _active : null);

        public Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not load the index for update.");

        public Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not add an index row.");
    }
}
