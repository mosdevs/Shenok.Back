using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shenok.Back.Api.Shared.Correlation;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api;
using Shenok.Back.Gateway.Api.Security;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class DownstreamClientTests
{
    [Theory]
    [InlineData(GatewayClients.Identity, GatewayTestSettings.IdentityBaseUrl)]
    [InlineData(GatewayClients.Content, GatewayTestSettings.ContentBaseUrl)]
    [InlineData(GatewayClients.Engagement, GatewayTestSettings.EngagementBaseUrl)]
    public async Task Client_forwards_the_internal_key_correlation_id_and_caller(string clientName, string baseUrl)
    {
        var userId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await using var provider = CreateProvider(out var recorder, clientName);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(GatewayClaims.Subject, userId.ToString("D")),
                new Claim(GatewayClaims.Roles, "Listener"),
                new Claim(GatewayClaims.Roles, " ContentCreator "),
                new Claim(GatewayClaims.Roles, "Admin,Listener"),
                new Claim(GatewayClaims.Verified, GatewayClaims.VerifiedTrue)
            ], authenticationType: "Bearer"))
        };
        http.Items[CorrelationId.ItemKey] = "corr-out";
        http.Request.Headers["Authorization"] = "Bearer inbound-token";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
        using var response = await client.GetAsync("users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, recorder.Calls);
        Assert.Equal(new Uri(baseUrl.TrimEnd('/') + "/users/me"), recorder.RequestUri);
        Assert.Equal(GatewayTestSettings.ApiKey, Header(recorder, InternalCaller.ApiKeyHeader));
        Assert.Equal("corr-out", Header(recorder, CorrelationId.HeaderName));
        Assert.Equal(userId.ToString("D"), Header(recorder, InternalCaller.UserIdHeader));
        Assert.Equal("Listener,ContentCreator", Header(recorder, InternalCaller.RolesHeader));
        Assert.False(recorder.Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task Anonymous_call_sends_the_internal_key_and_omits_the_caller()
    {
        await using var provider = CreateProvider(out var recorder, GatewayClients.Identity);
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(GatewayClients.Identity);
        using var response = await client.GetAsync("register");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(GatewayTestSettings.ApiKey, Header(recorder, InternalCaller.ApiKeyHeader));
        Assert.False(recorder.Headers.ContainsKey(InternalCaller.UserIdHeader));
        Assert.False(recorder.Headers.ContainsKey(InternalCaller.RolesHeader));
        Assert.False(recorder.Headers.ContainsKey(CorrelationId.HeaderName));
    }

    [Fact]
    public async Task Authenticated_caller_without_a_guid_subject_omits_the_user_headers()
    {
        await using var provider = CreateProvider(out var recorder, GatewayClients.Content);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(GatewayClaims.Subject, Guid.Empty.ToString("D")),
                new Claim(GatewayClaims.Roles, "Listener")
            ], authenticationType: "Bearer"))
        };
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(GatewayClients.Content);
        using var response = await client.GetAsync("internal/pages");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(GatewayTestSettings.ApiKey, Header(recorder, InternalCaller.ApiKeyHeader));
        Assert.False(recorder.Headers.ContainsKey(InternalCaller.UserIdHeader));
        Assert.False(recorder.Headers.ContainsKey(InternalCaller.RolesHeader));
    }

    private static ServiceProvider CreateProvider(out DownstreamRecorder recorder, string clientName)
    {
        recorder = new DownstreamRecorder();
        var recorded = recorder;
        var services = new ServiceCollection();
        services.AddLogging();
        GatewayHost.AddServices(services, GatewayTestSettings.Create());
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler(recorded));
        return services.BuildServiceProvider();
    }

    private static string Header(DownstreamRecorder recorder, string name) =>
        Assert.Single(recorder.Headers[name]);
}
