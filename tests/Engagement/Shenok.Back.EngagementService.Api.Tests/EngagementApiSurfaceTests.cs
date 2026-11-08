using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api;
using Shenok.Back.EngagementService.Api.Controllers;
using Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;
using Shenok.Back.EngagementService.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;
using Shenok.Back.EngagementService.Application.Listening.UseCases.ReportProgress;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;
using Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;

namespace Shenok.Back.EngagementService.Api.Tests;

public sealed class EngagementApiSurfaceTests
{
    private static readonly string[] ExpectedRoutes =
    [
        "POST /internal/scans",
        "POST /internal/pages/{pageId:guid}/follow",
        "DELETE /internal/pages/{pageId:guid}/follow",
        "POST /internal/progress",
        "POST /internal/listening-state",
        "POST /internal/can-play"
    ];

    [Fact]
    public void Every_endpoint_is_internal_and_controllers_take_use_cases()
    {
        var assembly = typeof(InternalScansController).Assembly;
        Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.Contains("WeatherForecast", StringComparison.Ordinal));
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            name => name.Name?.Contains("Akka", StringComparison.OrdinalIgnoreCase) == true);

        var controllers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();
        Assert.NotEmpty(controllers);

        var routes = new List<string>();
        foreach (var controller in controllers)
        {
            Assert.NotNull(controller.GetCustomAttribute<InternalServiceAttribute>());
            foreach (var parameter in controller.GetConstructors().Single().GetParameters())
            {
                Assert.True(
                    parameter.ParameterType.Name.EndsWith("UseCase", StringComparison.Ordinal)
                    || parameter.ParameterType == typeof(ICallerContextAccessor),
                    $"{controller.Name} depends on {parameter.ParameterType.Name}.");
            }

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var http = method.GetCustomAttributes().OfType<HttpMethodAttribute>().SingleOrDefault();
                if (http?.Template is null)
                    continue;

                Assert.NotNull(method.GetCustomAttribute<InternalServiceAttribute>() ?? controller.GetCustomAttribute<InternalServiceAttribute>());
                routes.Add($"{http.HttpMethods.Single()} {http.Template}");
            }
        }

        Assert.Equal(ExpectedRoutes.OrderBy(route => route, StringComparer.Ordinal), routes.OrderBy(route => route, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_use_case_request_has_a_validator()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = "engagement-test-key"
        }).Build();
        var services = new ServiceCollection();
        EngagementHost.AddServices(services, configuration, includeInfrastructure: false);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IValidator<RegisterScanRequest>>());
        Assert.NotNull(provider.GetService<IValidator<FollowPageRequest>>());
        Assert.NotNull(provider.GetService<IValidator<UnfollowPageRequest>>());
        Assert.NotNull(provider.GetService<IValidator<ReportProgressRequest>>());
        Assert.NotNull(provider.GetService<IValidator<GetListeningStateRequest>>());
        Assert.NotNull(provider.GetService<IValidator<CanPlayRequest>>());
    }
}
