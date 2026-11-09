namespace Shenok.Back.RecognitionService.Infrastructure.Messaging;

/// <summary>
/// The waveform bus. Content declares the same names. There is no shared contracts project.
/// </summary>
internal static class WaveformBrokerNames
{
    public const string Exchange = "shenok.waveforms";
    public const string Queue = "shenok.waveforms";
    public const string DeadLetterExchange = "shenok.waveforms.dlx";
    public const string DeadLetterQueue = "shenok.waveforms.dlq";
}
