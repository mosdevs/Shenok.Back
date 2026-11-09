using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Messaging;

/// <summary>
/// Applies waveform facts from <c>shenok.waveforms</c>. A duplicate event id is an ack.
/// A poison body is dead-lettered. A database failure is requeued.
/// </summary>
internal sealed class WaveformInboxConsumer(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<WaveformInboxConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ApplyWaveformRegisteredValidator RegisteredRules = new();
    private static readonly ApplyWaveformRevokedValidator RevokedRules = new();

    private readonly string _connectionString = RequireConnectionString(configuration);

    public bool IsConnected { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                IsConnected = false;
                logger.LogWarning(exception, "Waveform inbox lost the broker. Retrying in {DelaySeconds} seconds.", delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }

        IsConnected = false;
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_connectionString),
            AutomaticRecoveryEnabled = false,
            ConsumerDispatchConcurrency = 1,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
            ContinuationTimeout = TimeSpan.FromSeconds(10)
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: 1),
            stoppingToken);

        await WaveformTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 16, global: false, cancellationToken: stoppingToken);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        channel.ChannelShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(
            WaveformBrokerNames.Queue,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer: consumer,
            cancellationToken: stoppingToken);

        IsConnected = true;
        try
        {
            await Task.WhenAny(closed.Task, Task.Delay(Timeout.Infinite, stoppingToken));
            stoppingToken.ThrowIfCancellationRequested();
            throw new IOException("The waveform broker connection closed.");
        }
        finally
        {
            IsConnected = false;
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var body = delivery.Body.ToArray();
        var messageTypeName = delivery.BasicProperties.Type;
        if (string.IsNullOrEmpty(messageTypeName))
            messageTypeName = delivery.RoutingKey;

        Guid? loggedEventId = null;
        try
        {
            if (!TryRead(messageTypeName, delivery.BasicProperties.MessageId, body, out var registered, out var revoked, out var parsedType, out loggedEventId))
            {
                logger.LogWarning("Dead-lettered a waveform delivery. Event {EventId}.", loggedEventId);
                await SettleAsync(channel, delivery.DeliveryTag, ack: false, requeue: false);
                return;
            }

            if (parsedType == WaveformMessageType.WaveformRegistered)
            {
                var validation = await RegisteredRules.ValidateAsync(registered!, stoppingToken);
                if (!validation.IsValid)
                {
                    logger.LogWarning("Dead-lettered an invalid WaveformRegistered event {EventId}.", registered!.EventId);
                    await SettleAsync(channel, delivery.DeliveryTag, ack: false, requeue: false);
                    return;
                }

                await using var scope = scopes.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<ApplyWaveformRegisteredUseCase>();
                await useCase.ExecuteAsync(registered!, stoppingToken);
            }
            else
            {
                var validation = await RevokedRules.ValidateAsync(revoked!, stoppingToken);
                if (!validation.IsValid)
                {
                    logger.LogWarning("Dead-lettered an invalid WaveformRevoked event {EventId}.", revoked!.EventId);
                    await SettleAsync(channel, delivery.DeliveryTag, ack: false, requeue: false);
                    return;
                }

                await using var scope = scopes.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<ApplyWaveformRevokedUseCase>();
                await useCase.ExecuteAsync(revoked!, stoppingToken);
            }

            logger.LogDebug("Acknowledged waveform event {EventId}.", loggedEventId);
            await SettleAsync(channel, delivery.DeliveryTag, ack: true, requeue: false);
        }
        catch (InboxAlreadyProcessedException)
        {
            logger.LogDebug("Acknowledged a duplicate waveform event {EventId}.", loggedEventId);
            await SettleAsync(channel, delivery.DeliveryTag, ack: true, requeue: false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Requeueing waveform event {EventId} because the host is stopping.", loggedEventId);
            await SettleAsync(channel, delivery.DeliveryTag, ack: false, requeue: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Requeueing waveform event {EventId} after a failed apply.", loggedEventId);
            await SettleAsync(channel, delivery.DeliveryTag, ack: false, requeue: true);
        }
    }

    private async Task SettleAsync(IChannel channel, ulong deliveryTag, bool ack, bool requeue)
    {
        try
        {
            if (!channel.IsOpen)
                return;

            if (ack)
                await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None);
            else
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "The waveform channel closed before delivery {DeliveryTag} could be settled.", deliveryTag);
        }
    }

    private static bool TryRead(
        string? messageTypeName,
        string? messageId,
        byte[] body,
        out ApplyWaveformRegisteredRequest? registered,
        out ApplyWaveformRevokedRequest? revoked,
        out WaveformMessageType messageType,
        out Guid? eventId)
    {
        registered = null;
        revoked = null;
        messageType = default;
        eventId = null;
        if (body.Length == 0 || messageTypeName is not (nameof(WaveformMessageType.WaveformRegistered) or nameof(WaveformMessageType.WaveformRevoked)))
            return false;

        WaveformWirePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WaveformWirePayload>(body, Json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null)
            return false;

        eventId = payload.EventId;
        if (!string.IsNullOrEmpty(messageId)
            && (!Guid.TryParse(messageId, out var declared) || declared != payload.EventId))
        {
            return false;
        }

        messageType = Enum.Parse<WaveformMessageType>(messageTypeName);
        if (messageType == WaveformMessageType.WaveformRegistered)
        {
            registered = new ApplyWaveformRegisteredRequest(
                payload.EventId,
                payload.OccurredOn,
                payload.WaveformId,
                payload.WaveformCode,
                payload.LayoutVersion,
                payload.TargetType ?? string.Empty,
                payload.TargetId);
            return true;
        }

        revoked = new ApplyWaveformRevokedRequest(
            payload.EventId,
            payload.OccurredOn,
            payload.WaveformId,
            payload.WaveformCode,
            payload.LayoutVersion,
            payload.TargetType ?? string.Empty,
            payload.TargetId);
        return true;
    }

    private static string RequireConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:rabbitmq is required.");

        return connectionString;
    }

    private sealed class WaveformWirePayload
    {
        public Guid EventId { get; set; }
        public DateTime OccurredOn { get; set; }
        public Guid WaveformId { get; set; }
        public long WaveformCode { get; set; }
        public int LayoutVersion { get; set; }
        public string? TargetType { get; set; }
        public Guid TargetId { get; set; }
    }
}
