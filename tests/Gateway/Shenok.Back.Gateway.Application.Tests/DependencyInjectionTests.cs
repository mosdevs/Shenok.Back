using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Gateway.Application;

namespace Shenok.Back.Gateway.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddGatewayApplication_can_run_twice_without_adding_services()
    {
        var services = new ServiceCollection();

        services.AddGatewayApplication();
        var registered = services.Count;
        services.AddGatewayApplication();

        Assert.Equal(registered, services.Count);
    }

    [Fact]
    public void AddGatewayApplication_rejects_a_null_collection()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            DependencyInjection.AddGatewayApplication(null!));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void Application_assembly_stays_inside_its_boundary()
    {
        var names = typeof(DependencyInjection).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Contains("Shenok.Back.Application.Abstractions", names);
        Assert.DoesNotContain(names, name => name is not null && IsForbidden(name, allowAspNetCore: false));
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
}
