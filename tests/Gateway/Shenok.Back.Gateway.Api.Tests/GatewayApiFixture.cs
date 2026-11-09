using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class GatewayApiFixture : IAsyncLifetime
{
    private WebApplication? _app;

    private HttpClient? _client;

    public DownstreamRecorder Identity { get; } = new();

    public DownstreamRecorder Content { get; } = new();

    public DownstreamRecorder Engagement { get; } = new();

    public HttpClient Client => _client ?? throw new InvalidOperationException("The gateway test host is not started.");

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AuthProbeController).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(GatewayTestSettings.Values());

        GatewayHost.AddServices(builder.Services, builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly);
        builder.Services.AddHttpClient(GatewayClients.Identity)
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(Identity));
        builder.Services.AddHttpClient(GatewayClients.Content)
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(Content));
        builder.Services.AddHttpClient(GatewayClients.Engagement)
            .ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(Engagement));

        _app = builder.Build();
        GatewayHost.Map(_app);
        await _app.StartAsync();

        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        var address = addresses?.Addresses.FirstOrDefault(item => item.StartsWith("http://", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The gateway test host did not bind an HTTP address.");
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
        string? bearer = null,
        string? correlationId = null,
        string? internalKey = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (bearer is not null)
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        if (correlationId is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (internalKey is not null)
            request.Headers.TryAddWithoutValidation(InternalCaller.ApiKeyHeader, internalKey);

        return Client.SendAsync(request);
    }
}

[CollectionDefinition("Gateway API")]
public sealed class GatewayApiCollection : ICollectionFixture<GatewayApiFixture>;

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
