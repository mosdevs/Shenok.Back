using System.Security.Claims;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Scans.UseCases.ScanWaveform;

namespace Shenok.Back.Gateway.Api.Http;

internal static class ScanForm
{
    public const string ScanIdField = "scanId";

    public const string ImageField = "image";

    public static async Task<ScanFormRead> ReadAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GatewayCaller.RequireUserId(user);
        if (!request.HasFormContentType)
            return ScanFormRead.Ready(new ScanWaveformRequest(userId, string.Empty, string.Empty, []));

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return ScanFormRead.Ready(new ScanWaveformRequest(userId, string.Empty, string.Empty, []));
        }
        catch (BadHttpRequestException)
        {
            return ScanFormRead.Ready(new ScanWaveformRequest(userId, string.Empty, string.Empty, []));
        }

        var scanId = form[ScanIdField].ToString();
        var file = form.Files[ImageField];
        if (file is null || file.Length == 0)
            return ScanFormRead.Ready(new ScanWaveformRequest(userId, scanId, file?.ContentType ?? string.Empty, []));

        if (file.Length > GatewayLimits.MaxImageBytes)
            return ScanFormRead.TooLarge();

        var image = new byte[(int)file.Length];
        await using (var source = file.OpenReadStream())
        {
            var read = 0;
            while (read < image.Length)
            {
                var count = await source.ReadAsync(image.AsMemory(read), cancellationToken);
                if (count == 0)
                    break;

                read += count;
            }

            if (read != image.Length)
                return ScanFormRead.Ready(new ScanWaveformRequest(userId, scanId, file.ContentType, []));
        }

        return ScanFormRead.Ready(new ScanWaveformRequest(userId, scanId, file.ContentType, image));
    }
}

internal readonly struct ScanFormRead
{
    private ScanFormRead(ScanWaveformRequest? request, bool imageTooLarge)
    {
        Request = request;
        ImageTooLarge = imageTooLarge;
    }

    public ScanWaveformRequest? Request { get; }

    public bool ImageTooLarge { get; }

    public static ScanFormRead Ready(ScanWaveformRequest request) => new(request, false);

    public static ScanFormRead TooLarge() => new(null, true);
}
