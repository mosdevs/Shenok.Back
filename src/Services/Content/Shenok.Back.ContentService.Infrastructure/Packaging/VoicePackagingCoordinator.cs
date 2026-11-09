using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

/// <summary>
/// Polls the packaging queue and encodes those voices through one local Akka.Streams graph.
/// The stream's <c>SelectAsync</c> parallelism is the only scale knob: at most two FFmpeg processes.
/// Cluster sharding, routers, and pub/sub are the wrong tools here. A voice has no in-memory actor state;
/// Postgres is the queue, and this process also serves HTTP.
/// </summary>
public sealed class VoicePackagingCoordinator : ReceiveActor
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PackagingOptions _options;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private CancellationTokenSource? _run;
    private ICancelable? _nextPoll;

    public VoicePackagingCoordinator(IServiceScopeFactory scopes, IOptions<PackagingOptions> options)
    {
        _scopes = scopes;
        _options = options.Value;

        ReceiveAsync<PollPackaging>(PollAsync);
    }

    protected override void PreStart()
    {
        if (!_options.Enabled)
        {
            _log.Info("Voice packaging is disabled.");
            return;
        }

        _run = new CancellationTokenSource();
        ScheduleNext();
    }

    protected override void PostStop()
    {
        _nextPoll?.Cancel();
        _nextPoll = null;
        if (_run is not null)
        {
            _run.Cancel();
            _run.Dispose();
            _run = null;
        }

        base.PostStop();
    }

    private async Task PollAsync(PollPackaging _)
    {
        if (_run is null || _run.IsCancellationRequested)
            return;

        try
        {
            await PackageBatchAsync(_run.Token);
        }
        catch (Exception exception) when (_run is not null && !_run.IsCancellationRequested)
        {
            _log.Error(exception, "Voice packaging poll failed.");
        }

        if (_run is not null && !_run.IsCancellationRequested)
            ScheduleNext();
    }

    private async Task PackageBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VoiceId> voiceIds;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            voiceIds = await PackagingQueue.ListAsync(db, _options.BatchSize, cancellationToken);
        }

        if (voiceIds.Count == 0)
            return;

        await Source.From(voiceIds)
            .SelectAsync(_options.Parallelism, voiceId => PackageOneAsync(voiceId, cancellationToken))
            .RunWith(Sink.Ignore<NotUsed>(), Context.Materializer());
    }

    private async Task<NotUsed> PackageOneAsync(VoiceId voiceId, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = _scopes.CreateAsyncScope();
            var useCase = ActivatorUtilities.CreateInstance<PackageVoiceUseCase>(
                scope.ServiceProvider,
                PackagingCaller.Instance);
            await useCase.ExecuteAsync(new PackageVoiceRequest(voiceId.Value), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _log.Error(exception, "Voice {VoiceId} was not packaged.", voiceId.Value);
        }

        return NotUsed.Instance;
    }

    private void ScheduleNext()
    {
        _nextPoll?.Cancel();
        _nextPoll = Context.System.Scheduler.ScheduleTellOnceCancelable(
            TimeSpan.FromSeconds(_options.PollSeconds),
            Self,
            PollPackaging.Instance,
            Self);
    }

    private sealed class PollPackaging
    {
        public static readonly PollPackaging Instance = new();

        private PollPackaging()
        {
        }
    }
}
