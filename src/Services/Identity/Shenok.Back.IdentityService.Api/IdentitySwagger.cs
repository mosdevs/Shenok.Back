using Microsoft.OpenApi;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.IdentityService.Api;

internal static class IdentitySwagger
{
    public static IServiceCollection AddIdentitySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Shenok Identity",
                Version = "v1",
                Description = "Registration, OTP, sessions, and profiles. Send X-Internal-Key on every call except /health and /swagger. /users/me also needs X-User-Id."
            });

            options.AddSecurityDefinition("InternalKey", new OpenApiSecurityScheme
            {
                Name = InternalCaller.ApiKeyHeader,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Shared key forwarded by the Gateway."
            });
            options.AddSecurityDefinition("UserId", new OpenApiSecurityScheme
            {
                Name = InternalCaller.UserIdHeader,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Caller id. Required for /users/me. Use the userId returned by POST /register."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("InternalKey", document)] = [],
                [new OpenApiSecuritySchemeReference("UserId", document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseIdentitySwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Shenok Identity");
            options.DocumentTitle = "Shenok Identity";
            options.RoutePrefix = "swagger";
        });

        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

        return app;
    }
}
