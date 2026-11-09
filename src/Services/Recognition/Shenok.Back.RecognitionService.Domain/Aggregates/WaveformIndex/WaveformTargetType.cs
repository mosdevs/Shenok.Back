namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// What a decoded code points at. Recognition stores the pair and does not resolve a page to a voice.
/// </summary>
public enum WaveformTargetType
{
    Page = 0,
    Voice = 1
}
