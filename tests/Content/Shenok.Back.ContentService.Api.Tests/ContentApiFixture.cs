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
using Shenok.Back.Api.Shared;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.ContentService.Api.Controllers;
using Shenok.Back.ContentService.Application;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;
using ApplicationDependencyInjection = Shenok.Back.ContentService.Application.DependencyInjection;

namespace Shenok.Back.ContentService.Api.Tests;

public sealed class ContentApiFixture : IAsyncLifetime
{
    public const string ApiKey = "content-test-key";
    public const string GatewayBaseUrl = "https://gateway.test";

    private WebApplication? _app;

    public Guid CreatorId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The content API test host is not started.");

    public MemoryPages Pages { get; } = new();

    public MemoryVoices Voices { get; } = new();

    public MemoryMappings Mappings { get; } = new();

    public MemoryBlobs Blobs { get; } = new();

    public bool PackagingActorRegistered { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PagesController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = ApiKey,
            ["Playback:GatewayBaseUrl"] = GatewayBaseUrl,
            ["Playback:SigningKey"] = "content-api-test-signing-key-32bx",
            ["Playback:LifetimeSeconds"] = "600"
        });

        builder.Services.AddContentApplication();
        builder.Services.AddContentApi(builder.Configuration);
        builder.Services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
        builder.Services.AddContentSwagger();
        builder.Services.AddControllers().AddApplicationPart(typeof(PagesController).Assembly);
        builder.Services.AddSingleton<IPageRepository>(Pages);
        builder.Services.AddSingleton<IVoiceRepository>(Voices);
        builder.Services.AddSingleton<IWaveformMappingRepository>(Mappings);
        builder.Services.AddSingleton<IWaveformCodeAllocator>(new MemoryCodes());
        builder.Services.AddSingleton<IUnitOfWork, MemoryUnitOfWork>();
        builder.Services.AddSingleton<IBlobStorage>(Blobs);
        builder.Services.AddSingleton<IAudioPackager, MemoryPackager>();
        var counters = new MemoryCounterLedger();
        builder.Services.AddSingleton<ICounterLedger>(counters);
        builder.Services.AddSingleton<IPageCatalogReader>(new MemoryPageCatalogReader(Voices, counters));

        PackagingActorRegistered = builder.Services.Any(MentionsPackagingActor);

        _app = builder.Build();
        _app.UseContentSwagger();
        _app.UseShenokApi();
        _app.MapControllers();
        _app.MapHealthChecks("/health");
        await _app.StartAsync();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = addresses?.Addresses.FirstOrDefault(item => item.StartsWith("http://", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The content API test host did not bind an HTTP address.");
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
        string? roles = null,
        string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (apiKey)
            request.Headers.TryAddWithoutValidation(InternalCaller.ApiKeyHeader, ApiKey);
        if (userId is Guid id)
            request.Headers.TryAddWithoutValidation(InternalCaller.UserIdHeader, id.ToString("D"));
        if (roles is not null)
            request.Headers.TryAddWithoutValidation(InternalCaller.RolesHeader, roles);
        if (correlationId is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> AsCreatorAsync(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? userId = null,
        string roles = "ContentCreator",
        string? correlationId = null) =>
        SendAsync(method, path, body, apiKey: true, userId ?? CreatorId, roles, correlationId);

    public Page SeedDraft(string title = "Night Walks", Guid? ownerId = null)
    {
        var page = Page.CreateDraft(
            OwnerId.From(ownerId ?? CreatorId),
            PageTitle.Create(title),
            PageDescription.Create(null),
            null);
        Pages.Add(page);
        return page;
    }

    public Voice SeedPending(Page page, string title = "Episode")
    {
        var voice = CreatePendingVoice(page, title);
        page.AddVoiceReference(voice.Id, voice.Status);
        return voice;
    }

    public Voice SeedUnattachedPending(Page page, string title = "Episode") =>
        CreatePendingVoice(page, title);

    public Voice SeedReady(Page page, string title = "Episode")
    {
        var voice = SeedPending(page, title);
        voice.CompletePackagingSuccess(12, "hls/playlist.m3u8", "hls/seg/");
        return voice;
    }

    public Page SeedPublished(int voiceCount = 1)
    {
        var page = SeedDraft();
        var readiness = new Dictionary<VoiceId, PackagingStatus>(voiceCount);
        for (var index = 0; index < voiceCount; index++)
        {
            var voice = SeedReady(page, $"Episode {index + 1}");
            readiness[voice.Id] = PackagingStatus.Ready;
        }

        page.Publish(readiness);
        return page;
    }

    private Voice CreatePendingVoice(Page page, string title)
    {
        var voice = Voice.CreatePending(
            page.Id,
            VoiceTitle.Create(title),
            $"sources/{Guid.NewGuid():N}",
            "audio/wav",
            32);
        Voices.Add(voice);
        return voice;
    }

    public WaveformMapping SeedPageWaveform(Page page, bool revoke = false)
    {
        var mapping = WaveformMapping.RegisterForPage(
            WaveformCode.From(Mappings.NextSeedCode()),
            page.Id,
            LayoutVersion.V1,
            page.Status == PageStatus.Archived);
        if (revoke)
            mapping.Revoke();

        Mappings.Add(mapping);
        return mapping;
    }

    private static bool MentionsPackagingActor(ServiceDescriptor descriptor)
    {
        var names = new[]
        {
            descriptor.ServiceType.FullName,
            descriptor.ImplementationType?.FullName,
            descriptor.ImplementationInstance?.GetType().FullName
        };

        return names.Any(name => name is not null && (
            name.Contains("VoicePackagingCoordinator", StringComparison.Ordinal)
            || name.Contains("Akka.Actor.ActorSystem", StringComparison.Ordinal)
            || name.Contains("AkkaHostedService", StringComparison.Ordinal)));
    }
}

[CollectionDefinition("Content API")]
public sealed class ContentApiCollection : ICollectionFixture<ContentApiFixture>;

internal static class ApiJson
{
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    public static void AssertProblem(JsonElement body, HttpStatusCode status, string type)
    {
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.Equal(type, body.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }
}
