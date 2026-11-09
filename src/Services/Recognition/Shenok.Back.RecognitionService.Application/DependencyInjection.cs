using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions;

namespace Shenok.Back.RecognitionService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Recognition use cases.
    /// A second call does not register the same use case again.
    /// Validators are registered by <c>AddShenokApi</c>, not here.
    /// </summary>
    public static IServiceCollection AddRecognitionApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddUseCasesFrom(typeof(DependencyInjection).Assembly);
        return services;
    }
}
