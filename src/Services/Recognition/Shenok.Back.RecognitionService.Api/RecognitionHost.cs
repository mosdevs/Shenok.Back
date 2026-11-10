using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Shenok.Back.Api.Shared;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.RecognitionService.Application;
using Shenok.Back.RecognitionService.Infrastructure;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;
using ApplicationDependencyInjection = Shenok.Back.RecognitionService.Application.DependencyInjection;
using InfrastructureDependencyInjection = Shenok.Back.RecognitionService.Infrastructure.DependencyInjection;

namespace Shenok.Back.RecognitionService.Api;

/// <summary>
/// Process gate and HTTP composition for the internal Recognition host.
/// A blank API key stops startup before the process listens.
/// <c>/health</c> is a process check. <c>/ready</c> is the database, plus the broker when messaging is enabled.
/// </summary>
internal static class RecognitionHost
{
    public const string ReadyTag = InfrastructureDependencyInjection.ReadyTag;

    public static void RequireApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration[InternalCaller.ApiKeyConfigurationPath]))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");
    }

    public static void AddServices(IServiceCollection services, IConfiguration configuration, bool includeInfrastructure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        RequireApiKey(configuration);
        services.AddRecognitionApplication();
        if (includeInfrastructure)
            services.AddRecognitionInfrastructure(configuration);

        services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
        if (includeInfrastructure)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<RecognitionDbContext>(tags: [ReadyTag]);
        }
    }

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseShenokApi();
        app.Use(CatchOversizedBody);
        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains(ReadyTag)
        });
        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });
    }

    /// <summary>
    /// The shared exception middleware maps an unknown exception to HTTP 500.
    /// A body over 8 MB is the framework's 413, so this catch keeps that status.
    /// </summary>
    private static async Task CatchOversizedBody(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        }
    }
}
