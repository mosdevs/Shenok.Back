namespace Shenok.Back.Gateway.Application.Common;

/// <summary>
/// Target names Recognition returns. They match the Content waveform enum text.
/// </summary>
public static class WaveformTargets
{
    public const string Page = "Page";

    public const string Voice = "Voice";

    public static bool IsPage(string? value) =>
        string.Equals(value, Page, StringComparison.OrdinalIgnoreCase);

    public static bool IsVoice(string? value) =>
        string.Equals(value, Voice, StringComparison.OrdinalIgnoreCase);
}
