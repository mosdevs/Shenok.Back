using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Repositories;
using Shenok.Back.EngagementService.Infrastructure.Counters;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.EngagementService.Infrastructure.Persistence.DomainEvents;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Interceptors;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "engagement";

    public static IServiceCollection AddEngagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");

        services.AddScoped<DomainEventDispatcherInterceptor>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddDbContext<EngagementDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(provider.GetRequiredService<DomainEventDispatcherInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<EngagementDbContext>());
        services.AddScoped<IUserPageRelationshipRepository, UserPageRelationshipRepository>();
        services.AddScoped<IListeningRecordRepository, ListeningRecordRepository>();
        services.AddScoped<IScanReceiptRepository, ScanReceiptRepository>();
        services.AddScoped<IPlaySessionRepository, PlaySessionRepository>();

        services.AddOptions<ContentCounterOptions>()
            .Bind(configuration.GetSection(ContentCounterOptions.SectionName))
            .Validate(ContentCounterOptions.IsValid, "ContentCounters:BaseUrl is required.")
            .ValidateOnStart();
        services.AddHttpClient(ContentCounterOptions.HttpClientName, (provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ContentCounterOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                var apiKey = configuration[ContentCounterOptions.ApiKeyConfigurationPath];
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("InternalAuth:ApiKey is required.");

                client.DefaultRequestHeaders.TryAddWithoutValidation(ContentCounterOptions.ApiKeyHeader, apiKey);
            })
            .RedactLoggedHeaders([ContentCounterOptions.ApiKeyHeader]);
        services.AddScoped<ContentCounterPublisher>();
        services.AddScoped<IContentCounters, HttpContentCounters>();
        services.AddHostedService<CounterCommandDispatchService>();

        return services;
    }
}
