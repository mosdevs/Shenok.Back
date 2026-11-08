using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api;
using Shenok.Back.EngagementService.Api.Controllers;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Repositories;
using Shenok.Back.EngagementService.Infrastructure.Counters;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Api.Tests;

public sealed class EngagementApiFixture : IAsyncLifetime
{
    public const string ApiKey = "engagement-test-key";

    private WebApplication? _app;

    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The engagement API test host is not started.");

    public bool RegistersDatabase { get; private set; }

    public bool RegistersActorSystem { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(InternalScansController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = ApiKey,
            [$"{EngagementOptions.SectionName}:AutoFollowOnScan"] = "false",
            [EngagementDatabase.MigrateOnStartupKey] = "false"
        });

        var memory = new EngagementMemory();
        EngagementHost.AddServices(builder.Services, builder.Configuration, includeInfrastructure: false);
        builder.Services.AddControllers().AddApplicationPart(typeof(InternalScansController).Assembly);
        builder.Services.AddSingleton<IScanReceiptRepository>(new MemoryReceipts(memory));
        builder.Services.AddSingleton<IUserPageRelationshipRepository>(new MemoryRelationships(memory));
        builder.Services.AddSingleton<IListeningRecordRepository>(new MemoryListening(memory));
        builder.Services.AddSingleton<IPlaySessionRepository>(new MemorySessions(memory));
        builder.Services.AddSingleton<IUnitOfWork>(memory);
        builder.Services.AddSingleton<IContentCounters, NoOpContentCounters>();

        RegistersDatabase = builder.Services.Any(RegistersPostgres);
        RegistersActorSystem = builder.Services.Any(RegistersAkka);

        _app = builder.Build();
        EngagementHost.Map(_app);
        await _app.StartAsync();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = addresses?.Addresses.FirstOrDefault(item => item.StartsWith("http://", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The engagement API test host did not bind an HTTP address.");
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is null)
            return;

        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        bool apiKey = true,
        Guid? userId = null,
        string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (apiKey)
            request.Headers.TryAddWithoutValidation(InternalCaller.ApiKeyHeader, ApiKey);
        if (userId is Guid id)
            request.Headers.TryAddWithoutValidation(InternalCaller.UserIdHeader, id.ToString("D"));
        if (correlationId is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return Client.SendAsync(request);
    }

    private static bool RegistersPostgres(ServiceDescriptor descriptor)
    {
        var names = new[]
        {
            descriptor.ServiceType.FullName,
            descriptor.ImplementationType?.FullName,
            descriptor.ImplementationInstance?.GetType().FullName
        };

        return names.Any(name => name is not null && (
            name.Contains("EngagementDbContext", StringComparison.Ordinal)
            || name.Contains("Npgsql", StringComparison.Ordinal)));
    }

    private static bool RegistersAkka(ServiceDescriptor descriptor)
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
    }
}

[CollectionDefinition("Engagement API")]
public sealed class EngagementApiCollection : ICollectionFixture<EngagementApiFixture>;

internal static class ApiJson
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    public static void AssertProblem(HttpResponseMessage response, JsonElement body, HttpStatusCode status, string type)
    {
        Assert.Equal(ApiProblems.ProblemContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(type, body.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }
}
