using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Scans.UseCases.ScanWaveform;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class ScansController(ScanWaveformUseCase scan) : ControllerBase
{
    [HttpPost("/scans")]
    [RequestSizeLimit(GatewayLimits.MaxImageBytes + (256 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = GatewayLimits.MaxImageBytes + (256 * 1024))]
    public async Task<IActionResult> Scan(CancellationToken cancellationToken)
    {
        var read = await ScanForm.ReadAsync(Request, User, cancellationToken);
        if (read.ImageTooLarge)
            return GatewayHttp.FieldProblem(HttpContext, "Image", GatewayLimits.ImageSizeMessage);

        return await GatewayHttp.OkAsync(
            HttpContext,
            read.Request!,
            (request, token) => scan.ExecuteAsync(request, token),
            cancellationToken);
    }
}
