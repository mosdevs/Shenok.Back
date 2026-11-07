using Akka.Hosting;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

internal static class PackagingActorHosting
{
    public static IServiceCollection AddContentPackagingActor(this IServiceCollection services)
    {
        services.AddAkka("ContentPackaging", akka =>
        {
            akka.ConfigureLoggers(loggers =>
            {
                loggers.ClearLoggers();
                loggers.AddLoggerFactory();
            });
            akka.AddHocon(
                """
                akka.coordinated-shutdown.default-phase-timeout = 5s
                akka.coordinated-shutdown.exit-clr = off
                """,
                HoconAddMode.Prepend);
            akka.WithActors((system, registry, resolver) =>
            {
                var coordinator = system.ActorOf(
                    resolver.Props<VoicePackagingCoordinator>(),
                    "voice-packaging");
                registry.Register<VoicePackagingCoordinator>(coordinator);
            });
        });

        return services;
    }
}
