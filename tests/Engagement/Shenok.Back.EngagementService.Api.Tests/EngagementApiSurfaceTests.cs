using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Controllers;

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
}
