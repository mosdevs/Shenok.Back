namespace Shenok.Back.ContentService.Application.Common.Interfaces;

public abstract record AudioPackageResult
{
    private AudioPackageResult()
    {
    }

    /// <summary>
    /// Playlist names are relative. Segment URLs are not absolute and do not point at blob storage.
    /// </summary>
    public sealed record Succeeded(double DurationSeconds, string PlaylistStorageKey, string SegmentPrefix) : AudioPackageResult;

    public sealed record Failed(string Reason) : AudioPackageResult;
}

public interface IAudioPackager
{
    /// <summary>
    /// Probes the source and writes a private single-variant HLS package.
    /// Duration comes from the media. Do not write artifacts when the probed duration exceeds <paramref name="maxDurationSeconds"/>.
    /// </summary>
    Task<AudioPackageResult> PackageAsync(
        string sourceStorageKey,
        double maxDurationSeconds,
        CancellationToken cancellationToken);
}
