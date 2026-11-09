using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shenok.Back.Gateway.Api;
using Shenok.Back.Gateway.Api.Playback;
using Shenok.Back.Gateway.Api.Security;
using Shenok.Back.Gateway.Application;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class GatewayHostTests
{
    [Fact]
    public void AddServices_rejects_a_null_collection()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            GatewayHost.AddServices(null!, GatewayTestSettings.Create()));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddServices_rejects_a_null_configuration()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            GatewayHost.AddServices(new ServiceCollection(), null!));

        Assert.Equal("configuration", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddServices_rejects_a_blank_api_key_before_registering_services(string? apiKey)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, GatewayTestSettings.Create(apiKey: apiKey)));

        Assert.Equal("InternalAuth:ApiKey is required.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddServices_rejects_a_short_jwt_signing_key_before_registering_services()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, GatewayTestSettings.Create(signingKey: new string('k', 31))));

        Assert.Equal("Jwt:SigningKey must be at least 32 characters.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddServices_rejects_a_public_http_playback_url_before_registering_services()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ArgumentException>(() =>
            GatewayHost.AddServices(services, GatewayTestSettings.Create(playbackUrl: "http://example.com")));

        Assert.StartsWith("Gateway base URL must use https except on loopback.", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Theory]
    [InlineData(GatewayConfiguration.IdentityBaseUrlKey)]
    [InlineData(GatewayConfiguration.ContentBaseUrlKey)]
    [InlineData(GatewayConfiguration.EngagementBaseUrlKey)]
    public void AddServices_rejects_a_blank_downstream_url_before_registering_services(string key)
    {
        var services = new ServiceCollection();
        var values = GatewayTestSettings.Values();
        values[key] = " ";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void AddServices_rejects_a_non_positive_json_timeout(string timeout)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, GatewayTestSettings.Create(timeout: timeout)));

        Assert.Equal("Gateway:JsonTimeoutSeconds must be greater than zero.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddServices_rejects_a_non_numeric_json_timeout()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, GatewayTestSettings.Create(timeout: "soon")));

        Assert.Equal("Gateway:JsonTimeoutSeconds must be a positive number of seconds.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void Missing_issuer_and_audience_use_Identity_defaults()
    {
        var settings = GatewaySettings.Read(GatewayTestSettings.Create(includeIssuer: false, includeAudience: false));

        Assert.Equal("shenok", settings.JwtIssuer);
        Assert.Equal("shenok-mobile", settings.JwtAudience);
    }

    [Fact]
    public void Missing_timeout_defaults_to_ten_seconds()
    {
        var settings = GatewaySettings.Read(GatewayTestSettings.Create(includeTimeout: false));

        Assert.Equal(TimeSpan.FromSeconds(10), settings.JsonTimeout);
    }

    [Fact]
    public void AddServices_registers_jwt_playback_and_no_database_or_actor_system()
    {
        var services = new ServiceCollection();
        GatewayHost.AddServices(services, GatewayTestSettings.Create());

        Assert.DoesNotContain(services, RegistersInfrastructure);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<ProblemAuthorizationResultHandler>(
            provider.GetRequiredService<IAuthorizationMiddlewareResultHandler>());
        var playback = provider.GetRequiredService<GatewayPlaybackSettings>();
        Assert.Equal(GatewayTestSettings.PlaybackKey, playback.SigningKey);
        Assert.Equal("http://127.0.0.1:5080", playback.Authority);

        var jwt = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.False(jwt.MapInboundClaims);
        Assert.False(jwt.IncludeErrorDetails);
        Assert.Equal(GatewayConfiguration.DefaultJwtIssuer, jwt.TokenValidationParameters.ValidIssuer);
        Assert.Equal(GatewayConfiguration.DefaultJwtAudience, jwt.TokenValidationParameters.ValidAudience);
        Assert.Equal(GatewayConfiguration.JwtClockSkew, jwt.TokenValidationParameters.ClockSkew);
        Assert.Equal(GatewayPolicies.VerifiedCaller, "VerifiedCaller");
    }

    [Fact]
    public void Downstream_clients_use_the_configured_base_address_and_timeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        GatewayHost.AddServices(services, GatewayTestSettings.Create(timeout: "4"));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        AssertClient(factory, GatewayClients.Identity, GatewayTestSettings.IdentityBaseUrl);
        AssertClient(factory, GatewayClients.Content, GatewayTestSettings.ContentBaseUrl);
        AssertClient(factory, GatewayClients.Engagement, GatewayTestSettings.EngagementBaseUrl);
    }

    [Fact]
    public void Gateway_assemblies_do_not_reference_Akka_a_database_or_another_service()
    {
        AssertBoundary(typeof(GatewayHost).Assembly, allowAspNetCore: true);
        AssertBoundary(typeof(DependencyInjection).Assembly, allowAspNetCore: false);

        var api = typeof(GatewayHost).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.Contains("Shenok.Back.Gateway.Application", api);
        Assert.Contains("Shenok.Back.Api.Shared", api);
        Assert.Contains("Microsoft.AspNetCore.Authentication.JwtBearer", api);
    }

    private static void AssertClient(IHttpClientFactory factory, string name, string baseUrl)
    {
        var client = factory.CreateClient(name);
        Assert.Equal(new Uri(baseUrl.TrimEnd('/') + "/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(4), client.Timeout);
    }

    private static void AssertBoundary(Assembly assembly, bool allowAspNetCore)
    {
        var names = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(names, name => name is not null && IsForbidden(name, allowAspNetCore));
    }

    private static bool IsForbidden(string name, bool allowAspNetCore) =>
        name.StartsWith("Akka", StringComparison.Ordinal)
        || name.Contains("EntityFrameworkCore", StringComparison.Ordinal)
        || name.Contains("Npgsql", StringComparison.Ordinal)
        || name.Contains("IdentityService", StringComparison.Ordinal)
        || name.Contains("ContentService", StringComparison.Ordinal)
        || name.Contains("EngagementService", StringComparison.Ordinal)
        || name.Contains("RecognitionService", StringComparison.Ordinal)
        || (!allowAspNetCore && name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));

    private static bool RegistersInfrastructure(ServiceDescriptor descriptor)
    {
        var names = new[]
        {
            descriptor.ServiceType.FullName,
            descriptor.ImplementationType?.FullName,
            descriptor.ImplementationInstance?.GetType().FullName
        };

        return names.Any(name => name is not null && (
            name.Contains("Akka", StringComparison.Ordinal)
            || name.Contains("DbContext", StringComparison.Ordinal)
            || name.Contains("Npgsql", StringComparison.Ordinal)
            || name.Contains("EntityFrameworkCore", StringComparison.Ordinal)));
    }
}
