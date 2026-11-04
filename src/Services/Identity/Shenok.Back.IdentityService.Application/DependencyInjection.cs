using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.IdentityService.Application.Auth;

namespace Shenok.Back.IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<TokenIssuer>();
        services.AddScoped<OtpIssuer>();
        services.AddUseCasesFrom(typeof(DependencyInjection).Assembly);
        return services;
    }
}
