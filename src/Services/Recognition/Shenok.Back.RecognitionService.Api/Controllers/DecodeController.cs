using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;

namespace Shenok.Back.RecognitionService.Api.Controllers;

[ApiController]
[InternalService]
[Route("/internal/decode")]
public sealed class DecodeController(DecodeWaveformUseCase decode) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(DecodeWaveformValidator.MaxImageBytes)]
    public Task<DecodeWaveformResponse> Decode(
        [ModelBinder(typeof(RawWaveformImageBinder))] DecodeWaveformRequest request,
        CancellationToken cancellationToken)
        => decode.ExecuteAsync(request, cancellationToken);
}

/// <summary>
/// Binds the raw image. The body is read only for <c>image/jpeg</c> or <c>image/png</c> with no parameters.
/// A parameter suffix stays on the request so the validator rejects it.
/// </summary>
internal sealed class RawWaveformImageBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        var http = bindingContext.HttpContext;
        var contentType = http.Request.ContentType ?? string.Empty;
        if (http.Request.ContentLength is long length && length > DecodeWaveformValidator.MaxImageBytes)
            throw new BadHttpRequestException("The waveform image is larger than 8 MB.", StatusCodes.Status413PayloadTooLarge);

        byte[] image = [];
        if (DecodeWaveformValidator.IsAllowedContentType(contentType))
        {
            using var buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer, http.RequestAborted);
            image = buffer.ToArray();
        }

        bindingContext.Result = ModelBindingResult.Success(new DecodeWaveformRequest(contentType, image));
    }
}
