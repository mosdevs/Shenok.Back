namespace Shenok.Back.ContentService.Infrastructure.Packaging;

public sealed class PackagingOptions
{
    public const string SectionName = "Packaging";

    public bool Enabled { get; set; } = true;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    public int ProcessTimeoutSeconds { get; set; } = 1800;
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 8;

    public static bool IsValid(PackagingOptions options) =>
        IsToolPath(options.FfmpegPath)
        && IsToolPath(options.FfprobePath)
        && options.ProcessTimeoutSeconds is >= 30 and <= 3600
        && options.PollSeconds is >= 1 and <= 60
        && options.BatchSize is >= 1 and <= 8;

    private static bool IsToolPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 260 || path is "." or "..")
            return false;

        foreach (var character in path)
        {
            if (char.IsControl(character) || character is '"' or '&' or '|' or ';' or '`' or '$' or '<' or '>')
                return false;
        }

        return true;
    }
}
