using Microsoft.EntityFrameworkCore;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Repositories;
using Shenok.Back.RecognitionService.Infrastructure.Imaging;
using Shenok.Back.RecognitionService.Infrastructure.Messaging;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;
using Shenok.Back.RecognitionService.Infrastructure.Persistence.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "recognition";
    public const string RabbitConnectionStringName = "rabbitmq";
    public const string MessagingSection = "Messaging";

    public static IServiceCollection AddRecognitionInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");

        services.AddDbContext<RecognitionDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<RecognitionDbContext>());
        services.AddScoped<IWaveformIndexRepository, WaveformIndexRepository>();
        services.AddScoped<IInbox, InboxRepository>();
        services.AddSingleton<IWaveformDecoder, ImageSharpWaveformDecoder>();

        if (IsMessagingEnabled(configuration))
        {
            var rabbit = configuration.GetConnectionString(RabbitConnectionStringName);
            if (string.IsNullOrWhiteSpace(rabbit))
                throw new InvalidOperationException($"ConnectionStrings:{RabbitConnectionStringName} is required.");

            services.AddSingleton<WaveformInboxConsumer>();
            services.AddHostedService(provider => provider.GetRequiredService<WaveformInboxConsumer>());
            services.AddHealthChecks()
                .AddCheck<WaveformBrokerHealthCheck>("waveform-broker", tags: ["ready"]);
        }

        return services;
    }

    public static bool IsMessagingEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue($"{MessagingSection}:Enabled", false);
    }
}
