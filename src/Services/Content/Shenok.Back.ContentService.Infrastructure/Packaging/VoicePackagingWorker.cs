using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

public sealed class VoicePackagingWorker(
    IServiceScopeFactory scopes,
    IOptions<PackagingOptions> options,
    ILogger<VoicePackagingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Voice packaging is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PackageBatchAsync(settings.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Voice packaging poll failed.");
            }
        }
    }

    private async Task PackageBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<VoiceId> voiceIds;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            voiceIds = await db.Voices.AsNoTracking()
                .Where(voice => voice.Audio.PackagingStatus == PackagingStatus.Pending)
                .OrderBy(voice => voice.Id)
                .Select(voice => voice.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        foreach (var voiceId in voiceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var useCase = ActivatorUtilities.CreateInstance<PackageVoiceUseCase>(scope.ServiceProvider, new PackagingCaller());
                await useCase.ExecuteAsync(new PackageVoiceRequest(voiceId.Value), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Voice {VoiceId} was not packaged.", voiceId.Value);
            }
        }
    }

    private sealed class PackagingCaller : IContentCaller
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdmin => false;
        public bool IsContentCreator => false;
        public bool IsInternalService => true;
    }
}
