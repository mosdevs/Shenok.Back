using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Api.Shared.Correlation;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Api.Shared.Validation;
using Shenok.Back.Application.Abstractions;

namespace Shenok.Back.Api.Shared;

public static class ShenokApiBuilder
{
    public static IServiceCollection AddShenokApi(this IServiceCollection services, Assembly applicationAssembly, bool requireInternalApiKey = true)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICallerContextAccessor, CallerContextAccessor>();
        services.AddUseCasesFrom(applicationAssembly);
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        services.AddControllers(options =>
            {
                options.Filters.Add<InternalServiceScopeFilter>();
                options.Filters.Add<ValidationFilter>();
            })
            .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);
        services.AddHealthChecks();
        services.AddSingleton(new InternalApiKeyOptions(requireInternalApiKey));
        return services;
    }

    public static WebApplication UseShenokApi(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        var options = app.Services.GetRequiredService<InternalApiKeyOptions>();
        if (options.RequireInternalApiKey)
            app.UseMiddleware<InternalApiKeyMiddleware>();
        return app;
    }
}

public sealed record InternalApiKeyOptions(bool RequireInternalApiKey);
