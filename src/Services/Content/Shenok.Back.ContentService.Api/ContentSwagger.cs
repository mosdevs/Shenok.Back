using Microsoft.OpenApi;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.ContentService.Api;

internal static class ContentSwagger
{
    public static IServiceCollection AddContentSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Shenok Content",
                Version = "v1",
                Description = "Creator pages, voice upload, waveforms, and internal playback. Send X-Internal-Key on every call except /health and /swagger. Creator routes also need X-User-Id and X-User-Roles."
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
                Description = "Caller id. Required for creator routes. Internal routes authorize the service key and do not read this value."
            });
            options.AddSecurityDefinition("UserRoles", new OpenApiSecurityScheme
            {
                Name = InternalCaller.RolesHeader,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Comma-separated roles. Content reads Admin and ContentCreator."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("InternalKey", document)] = [],
                [new OpenApiSecuritySchemeReference("UserId", document)] = [],
                [new OpenApiSecuritySchemeReference("UserRoles", document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseContentSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Shenok Content");
            options.DocumentTitle = "Shenok Content";
            options.RoutePrefix = "swagger";
        });

        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        return app;
    }
}
