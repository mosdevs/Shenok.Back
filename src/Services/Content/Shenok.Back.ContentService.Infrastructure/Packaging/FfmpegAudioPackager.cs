using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Playback;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

public sealed class FfmpegAudioPackager(
    AzureBlobStorage storage,
    IOptions<PackagingOptions> options,
    ILogger<FfmpegAudioPackager> logger) : IAudioPackager
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<AudioPackageResult> PackageAsync(
        string sourceStorageKey,
        double maxDurationSeconds,
        CancellationToken cancellationToken)
    {
        if (!StorageKeyRules.IsObjectKey(sourceStorageKey))
            return new AudioPackageResult.Failed("source-invalid");
        if (!double.IsFinite(maxDurationSeconds) || maxDurationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDurationSeconds));

        var settings = options.Value;
        var directory = Path.Combine(Path.GetTempPath(), "shenok-content", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source");
        try
        {
            try
            {
                await storage.DownloadToFileAsync(sourceStorageKey, sourcePath, AudioAsset.MaxSourceSizeBytes, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return new AudioPackageResult.Failed("source-missing");
            }
            catch (InvalidDataException)
            {
                return new AudioPackageResult.Failed("source-invalid");
            }

            var duration = await ProbeDurationAsync(settings, sourcePath, cancellationToken);
            if (duration is null)
                return new AudioPackageResult.Failed("duration-unreadable");
            if (duration.Value > maxDurationSeconds)
                return new AudioPackageResult.Failed("duration-exceeded");

            var playlistPath = Path.Combine(directory, "index.m3u8");
            if (!await EncodeAsync(settings, sourcePath, playlistPath, cancellationToken))
                return new AudioPackageResult.Failed("package-failed");

            if (!TryReadPlaylist(playlistPath, directory, out var playlist, out var segmentNames))
                return new AudioPackageResult.Failed("playlist-invalid");

            var packageId = Guid.CreateVersion7().ToString("N");
            var prefix = $"packages/{packageId}/";
            var playlistKey = $"{prefix}index.m3u8";
            var uploaded = new List<string>(segmentNames.Count + 1);
            try
            {
                foreach (var segmentName in segmentNames)
                {
                    var segmentKey = prefix + segmentName;
                    await UploadFileAsync(Path.Combine(directory, segmentName), segmentKey, "video/mp2t", cancellationToken);
                    uploaded.Add(segmentKey);
                }

                await UploadTextAsync(playlist, playlistKey, "application/vnd.apple.mpegurl", cancellationToken);
                return new AudioPackageResult.Succeeded(duration.Value, playlistKey, prefix);
            }
            catch
            {
                await DeleteUploadedAsync(uploaded, playlistKey);
                throw;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Packager temporary files were not deleted.");
            }
        }
    }

    private async Task<double?> ProbeDurationAsync(PackagingOptions settings, string sourcePath, CancellationToken cancellationToken)
    {
        var result = await RunAsync(
            settings.FfprobePath,
            [
                "-v", "error",
                "-protocol_whitelist", "file,crypto",
                "-show_entries", "format=duration",
                "-of", "default=noprint_wrappers=1:nokey=1",
                sourcePath
            ],
            TimeSpan.FromSeconds(settings.ProcessTimeoutSeconds),
            64,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogWarning("ffprobe failed with exit code {ExitCode}.", result.ExitCode);
            return null;
        }

        var text = result.Output.Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
            || !double.IsFinite(duration)
            || duration <= 0)
            return null;

        return duration;
    }

    private async Task<bool> EncodeAsync(
        PackagingOptions settings,
        string sourcePath,
        string playlistPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(playlistPath) ?? throw new InvalidOperationException("Playlist directory is missing.");
        var result = await RunAsync(
            settings.FfmpegPath,
            [
                "-hide_banner",
                "-loglevel", "error",
                "-nostdin",
                "-protocol_whitelist", "file,crypto",
                "-y",
                "-i", sourcePath,
                "-vn",
                "-map_metadata", "-1",
                "-threads", "1",
                "-c:a", "aac",
                "-b:a", "128k",
                "-ac", "2",
                "-ar", "44100",
                "-hls_time", "10",
                "-hls_playlist_type", "vod",
                "-hls_flags", "independent_segments",
                "-hls_segment_filename", "seg_%05d.ts",
                "-f", "hls",
                "index.m3u8"
            ],
            TimeSpan.FromSeconds(settings.ProcessTimeoutSeconds),
            4096,
            cancellationToken,
            directory);

        if (result.ExitCode != 0)
            logger.LogWarning("ffmpeg failed with exit code {ExitCode}.", result.ExitCode);

        return result.ExitCode == 0 && File.Exists(playlistPath);
    }

    private static bool TryReadPlaylist(string playlistPath, string directory, out string playlist, out List<string> segmentNames)
    {
        playlist = string.Empty;
        segmentNames = [];
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(playlistPath);
        }
        catch (IOException)
        {
            return false;
        }

        if (bytes.Length == 0 || bytes.Length > HlsPlaylistSigner.MaxPlaylistChars)
            return false;

        try
        {
            playlist = Utf8.GetString(bytes);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var normalized = playlist.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (!normalized.StartsWith("#EXTM3U", StringComparison.Ordinal) || normalized.Contains("://", StringComparison.Ordinal))
            return false;

        var lines = normalized.Split('\n');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        var rewritten = new StringBuilder(normalized.Length);
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                rewritten.Append('\n');
                continue;
            }

            if (line.Contains("URI=", StringComparison.Ordinal) || line.StartsWith("#EXT-X-MAP", StringComparison.Ordinal))
                return false;
            if (line[0] == '#')
            {
                if (line.Contains('\\') || line.Contains("..", StringComparison.Ordinal))
                    return false;

                rewritten.Append(line).Append('\n');
                continue;
            }

            if (RelativeSegmentName(line, directory) is not string name || !seen.Add(name))
                return false;

            ordered.Add(name);
            var segmentPath = Path.Combine(directory, name);
            if (!File.Exists(segmentPath))
                return false;

            var length = new FileInfo(segmentPath).Length;
            if (length <= 0 || length > AudioAsset.MaxSourceSizeBytes)
                return false;

            rewritten.Append(name).Append('\n');
        }

        if (seen.Count is 0 or > HlsPlaylistSigner.MaxSegments)
            return false;

        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (!Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return false;

            var name = Path.GetFileName(file);
            if (name is "index.m3u8" or "source")
                continue;
            if (!seen.Contains(name))
                return false;
        }

        playlist = rewritten.ToString();
        segmentNames = ordered;
        return true;
    }

    private static string? RelativeSegmentName(string raw, string directory)
    {
        var value = raw.Trim();
        if (value.Length == 0 || value.Contains("://", StringComparison.Ordinal) || value.Contains("..", StringComparison.Ordinal))
            return null;

        var separated = value.Contains('/') || value.Contains('\\');
        var name = separated
            ? Path.GetFileName(value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))
            : value;
        if (!IsSegmentName(name))
            return null;
        if (!separated)
            return name;

        try
        {
            var full = Path.GetFullPath(value);
            var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? name : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsSegmentName(string name)
    {
        if (name.Length != 12 || !name.StartsWith("seg_", StringComparison.Ordinal) || !name.EndsWith(".ts", StringComparison.Ordinal))
            return false;

        for (var index = 4; index < 9; index++)
        {
            if (!char.IsAsciiDigit(name[index]))
                return false;
        }

        return true;
    }

    private async Task UploadFileAsync(string path, string storageKey, string contentType, CancellationToken cancellationToken)
    {
        var length = new FileInfo(path).Length;
        if (length <= 0 || length > AudioAsset.MaxSourceSizeBytes)
            throw new InvalidDataException("Packaged segment is outside the allowed size.");

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await storage.PutAsync(new BlobUpload(storageKey, stream, contentType, length), cancellationToken);
    }

    private async Task UploadTextAsync(string playlist, string storageKey, string contentType, CancellationToken cancellationToken)
    {
        var bytes = Utf8.GetBytes(playlist);
        if (bytes.Length == 0 || bytes.Length > HlsPlaylistSigner.MaxPlaylistChars)
            throw new InvalidDataException("Packaged playlist is outside the allowed size.");

        await using var stream = new MemoryStream(bytes, writable: false);
        await storage.PutAsync(new BlobUpload(storageKey, stream, contentType, bytes.Length), cancellationToken);
    }

    private async Task DeleteUploadedAsync(List<string> keys, string playlistKey)
    {
        if (!keys.Contains(playlistKey))
            keys.Add(playlistKey);

        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "A packaged object could not be deleted after a failed upload.");
            }
        }
    }

    private async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        int maxOutputChars,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Audio packager process did not start.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Audio packager executable was not found.", exception);
        }

        var stdout = ReadLimitedAsync(process.StandardOutput, maxOutputChars, cancellationToken);
        var stderr = ReadLimitedAsync(process.StandardError, 4096, cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            throw new TimeoutException("Audio packager process timed out.");
        }
        catch
        {
            Kill(process);
            throw;
        }

        var output = await stdout;
        var error = await stderr;
        if (error.Length > 0)
            logger.LogWarning("Audio packager wrote {ByteCount} bytes of diagnostics.", error.Length);

        return new ProcessResult(process.ExitCode, output);
    }

    private void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogDebug(exception, "Audio packager process had already exited.");
        }
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader, int maxChars, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var builder = new StringBuilder();
        var overflow = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            if (overflow)
                continue;

            var remaining = maxChars - builder.Length;
            if (read > remaining)
            {
                builder.Append(buffer, 0, remaining);
                overflow = true;
                continue;
            }

            builder.Append(buffer, 0, read);
        }

        if (overflow)
            throw new InvalidDataException("Audio packager output exceeded the limit.");

        return builder.ToString();
    }

    private readonly record struct ProcessResult(int ExitCode, string Output);
}
