namespace Shenok.Back.Gateway.Application.Common;

public static class GatewayLimits
{
    public const int ClientTokenLength = 64;

    /// <summary>
    /// Same ceiling Recognition uses for a decode image. The public scan contract stays inside it.
    /// </summary>
    public const int MaxImageBytes = 8 * 1024 * 1024;

    public static string ImageSizeMessage => $"Image must be between 1 and {MaxImageBytes} bytes.";

    public const string ReadyPackaging = "Ready";

    public const string MissDetail = "No waveform matched.";

    public const string LockedDetail = "The page is locked.";

    public const string PlaylistLinkDetail = "The playlist link is not valid.";

    public const string EmptyPageDetail = "The page has no voice.";

    public const string ArchivedStatus = "Archived";

    public const string ArchivedScanDetail = "Archived pages are hidden from new scans.";

    public const string AdminRole = "Admin";

    /// <summary>
    /// Same ceiling as Content's <c>AudioAsset.MaxSourceSizeBytes</c>, plus 64 KB for the multipart envelope.
    /// The Gateway does not reference Content, so the number is repeated here.
    /// </summary>
    public const long MaxSourceBytes = 100L * 1024 * 1024;

    public const long MaxUploadRequestBytes = MaxSourceBytes + 65_536;
}
