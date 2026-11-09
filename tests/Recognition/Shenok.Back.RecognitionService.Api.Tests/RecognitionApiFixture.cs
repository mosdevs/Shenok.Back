using System.Net;
using System.Net.Http.Headers;
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
using Shenok.Back.RecognitionService.Api.Controllers;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using WaveformIdentity = Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex.WaveformId;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Api.Tests;

public sealed class RecognitionApiFixture : IAsyncLifetime
{
    public const string ApiKey = "recognition-test-key";

    private WebApplication? _app;
    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The recognition API test host is not started.");

    public Guid WaveformId { get; } = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public Guid TargetId { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(DecodeController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = ApiKey
        });

        RecognitionHost.AddServices(builder.Services, builder.Configuration, includeInfrastructure: false);
        builder.Services.AddControllers().AddApplicationPart(typeof(DecodeController).Assembly);
        builder.Services.AddSingleton<IWaveformDecoder>(new ScriptedDecoder());
        builder.Services.AddSingleton<IWaveformIndexRepository>(new MemoryIndex(WaveformId, TargetId));
        builder.Services.AddSingleton<IInbox, UnusedInbox>();
        builder.Services.AddSingleton<IUnitOfWork, UnusedUnitOfWork>();

        _app = builder.Build();
        RecognitionHost.Map(_app);
        await _app.StartAsync();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = addresses?.Addresses.FirstOrDefault(item => item.StartsWith("http://", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The recognition API test host did not bind an HTTP address.");
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

    public Task<HttpResponseMessage> PostImageAsync(byte[] body, string contentType, bool apiKey = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/decode");
        if (apiKey)
            request.Headers.TryAddWithoutValidation(InternalCaller.ApiKeyHeader, ApiKey);

        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = content;
        return Client.SendAsync(request);
    }

    private sealed class ScriptedDecoder : IWaveformDecoder
    {
        public ValueTask<WaveformDecodeResult> DecodeAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
        {
            if (image.IsEmpty)
                return new ValueTask<WaveformDecodeResult>(new WaveformDecodeResult.Failure(WaveformDecodeResult.ImageUnreadable));

            WaveformDecodeResult result = image.Span[0] switch
            {
                1 => new WaveformDecodeResult.Success(42),
                2 => new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound),
                _ => new WaveformDecodeResult.Success(99)
            };
            return new ValueTask<WaveformDecodeResult>(result);
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
            => Task.FromResult(code == _active.Code && _active.IsActive ? _active : null);

        public Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default)
            => Task.FromResult(code == _active.Code ? _active : null);

        public Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not add an index row.");
    }

    private sealed class UnusedInbox : IInbox
    {
        public Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task AddAsync(Guid eventId, WaveformMessageType messageType, DateTime receivedOn, CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not write the inbox.");
    }

    private sealed class UnusedUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Decode must not save.");
    }
}

[CollectionDefinition("Recognition API")]
public sealed class RecognitionApiCollection : ICollectionFixture<RecognitionApiFixture>;

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
