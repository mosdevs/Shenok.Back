using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Domain.Repositories;
using Shenok.Back.IdentityService.Domain.Services;
using Shenok.Back.IdentityService.Infrastructure.Messaging;
using Shenok.Back.IdentityService.Infrastructure.Persistence;
using Shenok.Back.IdentityService.Infrastructure.Persistence.DomainEvents;
using Shenok.Back.IdentityService.Infrastructure.Persistence.Interceptors;
using Shenok.Back.IdentityService.Infrastructure.Persistence.Repositories;
using Shenok.Back.IdentityService.Infrastructure.Security;
using Shenok.Back.SharedKernel.DomainEvents;
using KavenegarClientOptions = Kavenegar.KavenegarOptions;

namespace Shenok.Back.IdentityService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("identity")
            ?? throw new InvalidOperationException("ConnectionStrings:identity is required.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<KavenegarOptions>(configuration.GetSection(KavenegarOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var kavenegar = configuration.GetSection(KavenegarOptions.SectionName).Get<KavenegarOptions>() ?? new KavenegarOptions();
        var smsConfigured = !string.IsNullOrWhiteSpace(kavenegar.ApiKey);
        if (!environment.IsDevelopment())
        {
            if (!smsConfigured)
                throw new InvalidOperationException("Kavenegar:ApiKey is required outside Development.");
            if (string.IsNullOrWhiteSpace(kavenegar.Template))
                throw new InvalidOperationException("Kavenegar:Template is required outside Development.");
            if (string.IsNullOrWhiteSpace(configuration["Email:WebhookUrl"]))
                throw new InvalidOperationException("Email:WebhookUrl is required outside Development.");
        }
        else if (smsConfigured && string.IsNullOrWhiteSpace(kavenegar.Template))
        {
            throw new InvalidOperationException("Kavenegar:Template is required when Kavenegar:ApiKey is set.");
        }

        services.AddScoped<DomainEventDispatcherInterceptor>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddDbContext<IdentityDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(provider.GetRequiredService<DomainEventDispatcherInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        if (environment.IsDevelopment())
        {
            services.AddSingleton<InMemoryEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<InMemoryEmailSender>());
        }
        else
        {
            services.AddHttpClient<IEmailSender, WebhookEmailSender>();
        }

        if (environment.IsDevelopment() && !smsConfigured)
        {
            services.AddSingleton<InMemorySmsSender>();
            services.AddSingleton<ISmsSender>(provider => provider.GetRequiredService<InMemorySmsSender>());
        }
        else
        {
            services.AddHttpClient<Kavenegar.IKavenegarApi, Kavenegar.KavenegarApi>((httpClient, serviceProvider) =>
            {
                var configured = serviceProvider.GetRequiredService<IOptions<KavenegarOptions>>().Value;
                return new Kavenegar.KavenegarApi(new KavenegarClientOptions { ApiKey = configured.ApiKey }, httpClient);
            });
            services.AddTransient<ISmsSender, KavenegarSmsSender>();
        }

        return services;
    }
}
