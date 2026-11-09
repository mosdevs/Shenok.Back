using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shenok.Back.RecognitionService.Infrastructure.Messaging;

/// <summary>
/// Ready probe for the waveform broker. Decode does not wait on this check.
/// </summary>
internal sealed class WaveformBrokerHealthCheck(WaveformInboxConsumer consumer) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(consumer.IsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The waveform broker is not connected."));
    }
}
