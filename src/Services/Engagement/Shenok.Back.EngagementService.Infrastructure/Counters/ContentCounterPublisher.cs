using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.EngagementService.Infrastructure.Counters;

/// <summary>
/// Posts committed counter commands to Content and marks them published after a 2xx response.
/// Content rejects a second delivery of the same idempotency key.
/// </summary>
public sealed class ContentCounterPublisher(
    EngagementDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<ContentCounterPublisher> logger)
{
    private const int BatchSize = 20;

    public async Task PublishSubscriberAsync(Guid commandId, Guid userId, Guid pageId, int delta, CancellationToken cancellationToken)
    {
        var command = await RequireAsync(commandId, cancellationToken);
        if (command.Kind != CounterCommand.SubscriberKind
            || command.UserId != userId
            || command.PageId != pageId
            || command.Delta != delta)
            throw new InvalidOperationException("The stored subscriber command does not match the follow.");

        if (command.PublishedOn is not null)
            return;

        await SendAsync(command, cancellationToken);
    }

    public async Task PublishPlayAsync(
        Guid commandId,
        string playSessionId,
        Guid voiceId,
        CancellationToken cancellationToken)
    {
        var command = await RequireAsync(commandId, cancellationToken);
        if (command.Kind != CounterCommand.PlayKind
            || command.PlaySessionId != playSessionId
            || command.VoiceId != voiceId)
            throw new InvalidOperationException("The stored play command does not match the report.");

        if (command.PublishedOn is not null)
            return;

        await SendAsync(command, cancellationToken);
    }

    public Task DeliverPendingSubscriberAsync(Guid userId, Guid pageId, CancellationToken cancellationToken) =>
        DeliverAsync(
            command => command.Kind == CounterCommand.SubscriberKind
                && command.UserId == userId
                && command.PageId == pageId
                && command.PublishedOn == null,
            cancellationToken);

    public Task DeliverPendingPlayAsync(string playSessionId, CancellationToken cancellationToken) =>
        DeliverAsync(
            command => command.Kind == CounterCommand.PlayKind
                && command.PlaySessionId == playSessionId
                && command.PublishedOn == null,
            cancellationToken);

    public async Task PublishOutstandingAsync(CancellationToken cancellationToken)
    {
        var pending = await db.CounterCommands.AsNoTracking()
            .Where(command => command.PublishedOn == null)
            .OrderBy(command => command.OccurredOn)
            .ThenBy(command => command.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var command in pending)
        {
            try
            {
                await SendAsync(command, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Counter command {CommandId} was not acknowledged.", command.Id);
            }
        }
    }

    private async Task DeliverAsync(
        System.Linq.Expressions.Expression<Func<CounterCommand, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var pending = await db.CounterCommands.AsNoTracking()
            .Where(predicate)
            .OrderBy(command => command.OccurredOn)
            .ThenBy(command => command.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var command in pending)
            await SendAsync(command, cancellationToken);
    }

    private async Task<CounterCommand> RequireAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var command = await db.CounterCommands.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == commandId, cancellationToken);
        if (command is null)
            throw new UnavailableException("The counter command was not stored.");

        return command;
    }

    private async Task SendAsync(CounterCommand command, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ContentCounterOptions.HttpClientName);
        using var response = await PostAsync(client, command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            await MarkPublishedAsync(command.Id, cancellationToken);
            return;
        }

        logger.LogWarning(
            "Content rejected counter command {CommandId} with status {StatusCode}.",
            command.Id,
            (int)response.StatusCode);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            throw new ConflictException("Content rejected the counter command.");

        throw new UnavailableException("Content counters are unavailable.");
    }

    private async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        CounterCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            if (command.Kind == CounterCommand.SubscriberKind)
            {
                return await client.PostAsJsonAsync(
                    $"internal/pages/{command.PageId:D}/subscribers",
                    new SubscriberBody(command.Delta, command.IdempotencyKey),
                    cancellationToken);
            }

            return await client.PostAsJsonAsync(
                $"internal/voices/{command.VoiceId:D}/plays",
                new PlayBody(command.PlaySessionId),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Counter command {CommandId} could not reach Content.", command.Id);
            throw new UnavailableException("Content counters are unavailable.");
        }
    }

    private Task<int> MarkPublishedAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var publishedOn = DateTime.UtcNow;
        return db.CounterCommands
            .Where(command => command.Id == commandId && command.PublishedOn == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(command => command.PublishedOn, publishedOn),
                cancellationToken);
    }

    private sealed record SubscriberBody(int Delta, string IdempotencyKey);

    private sealed record PlayBody(string? PlaySessionId);
}
