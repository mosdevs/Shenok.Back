using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Api.Http;
using Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;

namespace Shenok.Back.EngagementService.Api.Controllers;

[ApiController]
[InternalService]
public sealed class InternalScansController(
    RegisterScanUseCase registerScan,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/internal/scans")]
    public Task<IActionResult> Register(RegisterScanBody? body, CancellationToken cancellationToken)
    {
        var scan = body ?? new RegisterScanBody(null, Guid.Empty, Guid.Empty, Guid.Empty);
        return UseCaseHttp.OkAsync(
            HttpContext,
            new RegisterScanRequest(
                scan.ScanId ?? string.Empty,
                caller.GetRequired().UserId,
                scan.WaveformId,
                scan.VoiceId,
                scan.PageId),
            (request, token) => registerScan.ExecuteAsync(request, token),
            cancellationToken);
    }
}

/// <summary>
/// Scan body. The caller id is <c>X-User-Id</c>. A <c>userId</c> field in the JSON is ignored.
/// </summary>
public sealed record RegisterScanBody(string? ScanId, Guid WaveformId, Guid VoiceId, Guid PageId);
