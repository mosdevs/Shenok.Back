using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shenok.Back.RecognitionService.Application;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;
using Shenok.Back.RecognitionService.Infrastructure;
using Shenok.Back.RecognitionService.Infrastructure.Messaging;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class WaveformBusTests(InfrastructureFixture fixture)
{
    [RequiresDockerFact]
    public async Task Register_then_revoke_clears_the_active_hit_and_a_redelivery_does_not_insert()
    {
        Assert.False(string.IsNullOrWhiteSpace(fixture.RabbitConnectionString));
        var logs = new CollectingLoggerProvider();
        var code = 88_001L;
        var waveformId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var revokeId = Guid.NewGuid();
        var registeredAt = new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc);
        var registerJson = Payload(registerId, registeredAt, waveformId, code, targetId);
        var revokeJson = Payload(revokeId, registeredAt.AddMinutes(2), waveformId, code, targetId);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:recognition"] = fixture.PostgresConnectionString,
            ["ConnectionStrings:rabbitmq"] = fixture.RabbitConnectionString,
            ["Messaging:Enabled"] = "true"
        });
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddProvider(logs);
        builder.Services.AddRecognitionApplication();
        builder.Services.AddRecognitionInfrastructure(builder.Configuration);

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var consumer = host.Services.GetRequiredService<WaveformInboxConsumer>();
            await WaitUntilAsync(() => consumer.IsConnected, TimeSpan.FromSeconds(20));

            await PublishAsync(nameof(WaveformMessageType.WaveformRegistered), registerId, registerJson);
            await WaitUntilAsync(async () => await ActiveAsync(code) is not null, TimeSpan.FromSeconds(20));

            await PublishAsync(nameof(WaveformMessageType.WaveformRevoked), revokeId, revokeJson);
            await WaitUntilAsync(
                async () => await ActiveAsync(code) is null && await CountIndexAsync(code) == 1,
                TimeSpan.FromSeconds(20));

            var acknowledged = AckCount(logs, registerId);
            await PublishAsync(nameof(WaveformMessageType.WaveformRegistered), registerId, registerJson);
            await WaitUntilAsync(() => AckCount(logs, registerId) > acknowledged, TimeSpan.FromSeconds(20));

            Assert.Null(await ActiveAsync(code));
            Assert.Equal(1, await CountIndexAsync(code));
            Assert.Equal(1, await CountInboxAsync(registerId));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private async Task PublishAsync(string messageType, Guid eventId, string json)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(fixture.RabbitConnectionString!),
            AutomaticRecoveryEnabled = false
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await WaveformTopology.DeclareAsync(channel, CancellationToken.None);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = messageType,
            MessageId = eventId.ToString("D")
        };
        await channel.BasicPublishAsync(
            WaveformBrokerNames.Exchange,
            messageType,
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(json));
    }

    private async Task<WaveformIndexEntry?> ActiveAsync(long code)
    {
        await using var db = fixture.CreateContext();
        var stored = WaveformCode.From(code);
        return await db.WaveformIndex.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Code == stored && entry.Status == WaveformIndexStatus.Active);
    }

    private async Task<int> CountIndexAsync(long code)
    {
        await using var db = fixture.CreateContext();
        var codes = await db.WaveformIndex.AsNoTracking().Select(entry => entry.Code).ToListAsync();
        return codes.Count(item => item.Value == code);
    }

    private async Task<int> CountInboxAsync(Guid eventId)
    {
        await using var db = fixture.CreateContext();
        return await db.InboxMessages.CountAsync(message => message.EventId == eventId);
    }

    private static int AckCount(CollectingLoggerProvider logs, Guid eventId)
    {
        var id = eventId.ToString("D");
        return logs.Messages.Count(line =>
            line.Contains("Acknowledged", StringComparison.Ordinal)
            && line.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    private static string Payload(Guid eventId, DateTime occurredOn, Guid waveformId, long code, Guid targetId)
        => JsonSerializer.Serialize(
            new
            {
                eventId,
                occurredOn,
                waveformId,
                waveformCode = code,
                layoutVersion = LayoutVersion.Version1,
                targetType = nameof(WaveformTargetType.Page),
                targetId
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("The waveform bus did not reach the expected state.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException("The waveform bus did not reach the expected state.");
    }

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CollectingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
