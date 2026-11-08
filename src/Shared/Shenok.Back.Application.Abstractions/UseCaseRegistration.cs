using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Shenok.Back.Application.Abstractions;

public static class UseCaseRegistration
{
    public static IServiceCollection AddUseCasesFrom(this IServiceCollection services, Assembly assembly)
    {
        var useCaseTypes = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(IsUseCase));

        foreach (var type in useCaseTypes)
        {
            if (services.Any(descriptor => descriptor.ServiceType == type))
                continue;

            services.AddScoped(type);
            foreach (var service in type.GetInterfaces().Where(IsUseCase))
                services.AddScoped(service, type);
        }

        return services;
    }

    private static bool IsUseCase(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(IUseCase<,>)
            || type.GetGenericTypeDefinition() == typeof(IUseCase<>));
}
