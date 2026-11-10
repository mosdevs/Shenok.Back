using System.Text;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal static class ProxiedResults
{
    public static IActionResult From(ProxiedResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        IActionResult result = response.Body.Length == 0
            ? new StatusCodeResult(response.StatusCode)
            : new ContentResult
            {
                StatusCode = response.StatusCode,
                ContentType = string.IsNullOrWhiteSpace(response.ContentType)
                    ? "application/octet-stream"
                    : response.ContentType,
                Content = Encoding.UTF8.GetString(response.Body)
            };

        if (string.IsNullOrEmpty(response.Location))
            return result;

        return new LocationResult(result, response.Location);
    }

    private sealed class LocationResult(IActionResult inner, string location) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.Headers.Location = location;
            return inner.ExecuteResultAsync(context);
        }
    }
}
