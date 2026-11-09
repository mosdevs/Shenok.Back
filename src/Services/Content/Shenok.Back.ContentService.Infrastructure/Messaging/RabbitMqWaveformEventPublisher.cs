using System.Text;
using RabbitMQ.Client;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Messaging;

/// <summary>
/// Publishes waveform facts to the durable direct exchange. A confirm must arrive before the call returns.
/// A nack or an unroutable message throws, and the outbox row stays unpublished.
/// </summary>
internal sealed class RabbitMqWaveformEventPublisher : IWaveformEventPublisher, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqWaveformEventPublisher(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:rabbitmq is required.");

        _connectionString = connectionString;
    }

    public async Task PublishAsync(Guid eventId, string messageType, string payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageType);
        ArgumentNullException.ThrowIfNull(payload);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await ChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Type = messageType,
                MessageId = eventId.ToString("D")
            };
            var body = Encoding.UTF8.GetBytes(payload);
            await channel.BasicPublishAsync(
                WaveformBrokerNames.Exchange,
                messageType,
                mandatory: true,
                properties,
                body,
                cancellationToken);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _gate.Dispose();
    }

    private async Task<IChannel> ChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await ResetAsync();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_connectionString),
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
            ContinuationTimeout = TimeSpan.FromSeconds(10)
        };
        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: 1),
            cancellationToken);
        await DeclareAsync(_channel, cancellationToken);
        return _channel;
    }

    private static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            WaveformBrokerNames.DeadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            WaveformBrokerNames.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            WaveformBrokerNames.DeadLetterQueue,
            WaveformBrokerNames.DeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            WaveformBrokerNames.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = WaveformBrokerNames.DeadLetterExchange
        };
        await channel.QueueDeclareAsync(
            WaveformBrokerNames.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            WaveformBrokerNames.Queue,
            WaveformBrokerNames.Exchange,
            OutboxMessage.RegisteredType,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            WaveformBrokerNames.Queue,
            WaveformBrokerNames.Exchange,
            OutboxMessage.RevokedType,
            cancellationToken: cancellationToken);
    }

    private async Task ResetAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
