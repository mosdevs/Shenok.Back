using Microsoft.EntityFrameworkCore;
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
        services.AddSingleton<IContentCounters, NoOpContentCounters>();

        return services;
    }
}
