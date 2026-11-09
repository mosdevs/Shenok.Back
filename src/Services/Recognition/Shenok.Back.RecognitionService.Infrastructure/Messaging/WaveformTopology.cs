using RabbitMQ.Client;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Messaging;

internal static class WaveformTopology
{
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
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
            nameof(WaveformMessageType.WaveformRegistered),
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            WaveformBrokerNames.Queue,
            WaveformBrokerNames.Exchange,
            nameof(WaveformMessageType.WaveformRevoked),
            cancellationToken: cancellationToken);
    }
}
