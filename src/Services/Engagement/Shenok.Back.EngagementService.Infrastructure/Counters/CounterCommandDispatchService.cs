using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Shenok.Back.EngagementService.Infrastructure.Counters;

/// <summary>
/// Retries counter commands that were committed and not acknowledged.
/// A follow or progress call also delivers its own command; this covers a crash after that commit.
/// </summary>
internal sealed class CounterCommandDispatchService(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentCounterOptions> options,
    ILogger<CounterCommandDispatchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, options.Value.DispatchSeconds));
        using var timer = new PeriodicTimer(delay);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var publisher = scope.ServiceProvider.GetRequiredService<ContentCounterPublisher>();
                await publisher.PublishOutstandingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Counter command dispatch failed.");
            }
        }
    }
}
