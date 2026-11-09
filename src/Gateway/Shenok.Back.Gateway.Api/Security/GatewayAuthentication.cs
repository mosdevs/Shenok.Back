using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Shenok.Back.Gateway.Api;

namespace Shenok.Back.Gateway.Api.Security;

internal static class GatewayAuthentication
{
    public static void Add(IServiceCollection services, GatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = settings.JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = GatewayConfiguration.JwtClockSkew,
                    NameClaimType = GatewayClaims.Username,
                    RoleClaimType = GatewayClaims.Roles
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst(GatewayClaims.Subject)?.Value;
                        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
                            context.Fail("The access token subject is missing.");

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        return context.Response.HasStarted
                            ? Task.CompletedTask
                            : GatewayAuthResponses.WriteUnauthorizedAsync(context.HttpContext);
                    }
                };
            });

        var verified = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(GatewayClaims.Verified, GatewayClaims.VerifiedTrue)
            .Build();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(GatewayPolicies.VerifiedCaller, verified);
            options.DefaultPolicy = verified;
            options.FallbackPolicy = verified;
        });
    }
}
