using System.Net;
using Shenok.Back.Api.Shared.Correlation;

namespace Shenok.Back.ContentService.Api.Tests;

[Collection("Content API")]
public sealed class PipelineTests(ContentApiFixture api)
{
    [Fact]
    public async Task Health_is_open_without_the_internal_key()
    {
        var response = await api.SendAsync(HttpMethod.Get, "/health", apiKey: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_internal_key_is_401_and_echoes_the_correlation_id()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/pages",
            new { title = "Night Walks" },
            apiKey: false,
            correlationId: "corr-missing-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("corr-missing-key", response.Headers.GetValues(CorrelationId.HeaderName).Single());
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
    }

    [Fact]
    public async Task Wrong_internal_key_is_rejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Internal-Key", "not-the-key");
        var health = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var pages = new HttpRequestMessage(HttpMethod.Get, "/pages/11111111-1111-1111-1111-111111111111");
        pages.Headers.TryAddWithoutValidation("X-Internal-Key", "x");
        var response = await api.Client.SendAsync(pages);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal("The internal API key is missing.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Swagger_document_is_open_and_lists_creator_and_internal_routes()
    {
        var response = await api.SendAsync(HttpMethod.Get, "/swagger/v1/swagger.json", apiKey: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("Shenok Content", document, StringComparison.Ordinal);
        Assert.Contains("/internal/playback", document, StringComparison.Ordinal);
        Assert.Contains("/internal/pages/{pageId}/subscribers", document, StringComparison.Ordinal);
        Assert.Contains("/pages/{pageId}/publish", document, StringComparison.Ordinal);
        Assert.Contains("X-Internal-Key", document, StringComparison.Ordinal);
        Assert.Contains("X-User-Id", document, StringComparison.Ordinal);
        Assert.Contains("X-User-Roles", document, StringComparison.Ordinal);
        Assert.Contains("not from the URL prefix", document, StringComparison.Ordinal);
        Assert.Contains("not from the URL prefix", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_is_not_a_public_entry()
    {
        var anonymous = await api.SendAsync(HttpMethod.Get, "/", apiKey: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var signed = await api.SendAsync(HttpMethod.Get, "/");
        Assert.Equal(HttpStatusCode.NotFound, signed.StatusCode);
        Assert.Null(signed.Headers.Location);
    }

    [Fact]
    public void Http_host_does_not_register_the_packaging_actor()
    {
        Assert.False(api.PackagingActorRegistered);
    }
}
