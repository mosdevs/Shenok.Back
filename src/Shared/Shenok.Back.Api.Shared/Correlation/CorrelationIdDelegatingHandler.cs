using Microsoft.AspNetCore.Http;

namespace Shenok.Back.Api.Shared.Correlation;

public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext?.Items.TryGetValue(CorrelationId.ItemKey, out var value) == true
            && value is string correlationId
            && !request.Headers.Contains(CorrelationId.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
