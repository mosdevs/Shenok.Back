using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Application.Common;

namespace Shenok.Back.EngagementService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Engagement use cases and binds <see cref="EngagementOptions"/>.
    /// A second call does not register the same use case again.
    /// Validators are registered by <c>AddShenokApi</c>, not here.
    /// </summary>
    public static IServiceCollection AddEngagementApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<EngagementOptions>()
            .Bind(configuration.GetSection(EngagementOptions.SectionName));
        services.AddUseCasesFrom(typeof(DependencyInjection).Assembly);
        return services;
    }
}
