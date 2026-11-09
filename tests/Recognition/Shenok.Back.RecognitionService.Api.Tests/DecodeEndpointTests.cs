using System.Net;
using System.Reflection;
using Shenok.Back.Api.Shared.Errors;
using Shenok.Back.RecognitionService.Api.Controllers;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;

namespace Shenok.Back.RecognitionService.Api.Tests;

[Collection("Recognition API")]
public sealed class DecodeEndpointTests(RecognitionApiFixture api)
{
    [Fact]
    public async Task Missing_key_is_401_and_health_is_open()
    {
        var denied = await api.PostImageAsync([1], "image/jpeg", apiKey: false);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var deniedBody = await ApiJson.ReadAsync(denied);
        ApiJson.AssertProblem(denied, deniedBody, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("The internal API key is missing.", deniedBody.GetProperty("detail").GetString());

        var health = await api.Client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("Healthy", await health.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_active_code_returns_the_three_fields()
    {
        var response = await api.PostImageAsync([1, 2, 3], "image/jpeg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal(api.WaveformId, body.GetProperty("waveformId").GetGuid());
        Assert.Equal("Page", body.GetProperty("targetType").GetString());
        Assert.Equal(api.TargetId, body.GetProperty("targetId").GetGuid());
        Assert.False(body.TryGetProperty("waveformCode", out _));
    }

    [Fact]
    public async Task A_decoder_failure_is_a_miss()
    {
        var response = await api.PostImageAsync([2], "image/jpeg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.NotFound, "https://shenok.dev/problems/not-found");
        Assert.Equal(DecodeWaveformUseCase.MissDetail, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task An_unknown_code_is_the_same_miss()
    {
        var response = await api.PostImageAsync([3], "image/jpeg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal(DecodeWaveformUseCase.MissDetail, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_gif_is_400_and_an_oversized_body_is_413()
    {
        var gif = await api.PostImageAsync([1], "image/gif");
        Assert.Equal(HttpStatusCode.BadRequest, gif.StatusCode);
        var gifBody = await ApiJson.ReadAsync(gif);
        ApiJson.AssertProblem(gif, gifBody, HttpStatusCode.BadRequest, ApiProblems.ValidationType);

        var oversized = new byte[DecodeWaveformValidator.MaxImageBytes + 1];
        oversized[0] = 1;
        var tooLarge = await api.PostImageAsync(oversized, "image/jpeg");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public void Recognition_does_not_reference_akka()
    {
        AssertNoAkka(typeof(DecodeController).Assembly);
        AssertNoAkka(typeof(RecognitionDbContext).Assembly);
    }

    private static void AssertNoAkka(Assembly assembly)
    {
        var names = assembly.GetReferencedAssemblies().Select(name => name.Name);
        Assert.DoesNotContain(names, name => name is not null && name.StartsWith("Akka", StringComparison.Ordinal));
    }
}
