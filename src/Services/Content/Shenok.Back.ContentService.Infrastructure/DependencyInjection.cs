using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;
using Shenok.Back.ContentService.Infrastructure.Packaging;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.DomainEvents;
using Shenok.Back.ContentService.Infrastructure.Persistence.Interceptors;
using Shenok.Back.ContentService.Infrastructure.Persistence.Counters;
using Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;
using Shenok.Back.ContentService.Infrastructure.Storage;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddContentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("content")
            ?? throw new InvalidOperationException("ConnectionStrings:content is required.");
        var blobConnectionString = configuration.GetConnectionString("blobs")
            ?? throw new InvalidOperationException("ConnectionStrings:blobs is required.");

        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .Validate(BlobStorageOptions.IsValid, "BlobStorage:ContainerName is invalid.")
            .ValidateOnStart();
        services.AddOptions<PackagingOptions>()
            .Bind(configuration.GetSection(PackagingOptions.SectionName))
            .Validate(PackagingOptions.IsValid, "Packaging configuration is invalid.")
            .ValidateOnStart();

        services.AddScoped<DomainEventDispatcherInterceptor>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddDbContext<ContentDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.AddInterceptors(provider.GetRequiredService<DomainEventDispatcherInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ContentDbContext>());
        services.AddScoped<ICounterLedger, PostgresCounterLedger>();
        services.AddScoped<IPageRepository, PageRepository>();
        services.AddScoped<IVoiceRepository, VoiceRepository>();
        services.AddScoped<IWaveformMappingRepository, WaveformMappingRepository>();
        services.AddScoped<IWaveformCodeAllocator, PostgresWaveformCodeAllocator>();

        services.AddSingleton(new BlobServiceClient(blobConnectionString));
        services.AddSingleton<AzureBlobStorage>();
        services.AddSingleton<IBlobStorage>(provider => provider.GetRequiredService<AzureBlobStorage>());
        services.AddSingleton<IAudioPackager, FfmpegAudioPackager>();
        if (IsPackagingEnabled(configuration))
            services.AddContentPackagingActor();

        return services;
    }

    private static bool IsPackagingEnabled(IConfiguration configuration)
    {
        var packaging = configuration.GetSection(PackagingOptions.SectionName).Get<PackagingOptions>()
            ?? new PackagingOptions();
        return packaging.Enabled;
    }
}
