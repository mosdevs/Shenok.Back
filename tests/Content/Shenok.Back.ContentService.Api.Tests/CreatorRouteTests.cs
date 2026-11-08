using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Shenok.Back.ContentService.Api.Tests;

[Collection("Content API")]
public sealed class CreatorRouteTests(ContentApiFixture api)
{
    [Fact]
    public async Task Create_draft_returns_201_and_the_caller_becomes_the_owner()
    {
        var response = await api.AsCreatorAsync(HttpMethod.Post, "/pages", new
        {
            title = "Night Walks",
            description = "Field recordings",
            coverImageUrl = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        var pageId = body.GetProperty("pageId").GetGuid();
        Assert.Equal($"/pages/{pageId:D}", response.Headers.Location?.OriginalString);
        Assert.Equal(api.CreatorId, body.GetProperty("ownerId").GetGuid());
        Assert.Equal("Draft", body.GetProperty("status").GetString());
        Assert.Equal("Night Walks", body.GetProperty("title").GetString());
        Assert.Empty(body.GetProperty("voices").EnumerateArray());
    }

    [Fact]
    public async Task Missing_caller_cannot_create_a_page()
    {
        var before = api.Pages.Count;
        var response = await api.SendAsync(HttpMethod.Post, "/pages", new { title = "Night Walks" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal(before, api.Pages.Count);
    }

    [Fact]
    public async Task Role_names_are_case_sensitive()
    {
        var response = await api.AsCreatorAsync(
            HttpMethod.Post,
            "/pages",
            new { title = "Night Walks" },
            roles: "admin, contentcreator");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.Forbidden, "https://shenok.dev/problems/forbidden");
    }

    [Fact]
    public async Task A_role_that_is_not_creator_or_admin_is_forbidden()
    {
        var response = await api.AsCreatorAsync(
            HttpMethod.Post,
            "/pages",
            new { title = "Night Walks" },
            roles: "Listener");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.Forbidden, "https://shenok.dev/problems/forbidden");
    }

    [Fact]
    public async Task Blank_title_is_a_validation_problem_and_stores_nothing()
    {
        var before = api.Pages.Count;
        var response = await api.AsCreatorAsync(HttpMethod.Post, "/pages", new { title = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.Contains("Title", body.GetProperty("errors").EnumerateObject().Select(property => property.Name));
        Assert.Equal(before, api.Pages.Count);
    }

    [Fact]
    public async Task Admin_can_rename_another_owners_page()
    {
        var page = api.SeedDraft("Original", ownerId: Guid.NewGuid());

        var response = await api.AsCreatorAsync(
            HttpMethod.Put,
            $"/pages/{page.Id.Value:D}",
            new { title = "Renamed by admin", description = "Kept", coverImageUrl = (string?)null },
            userId: Guid.NewGuid(),
            roles: "Admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal("Renamed by admin", body.GetProperty("title").GetString());
        Assert.Equal("Kept", body.GetProperty("description").GetString());
        Assert.Equal(page.OwnerId.Value, body.GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task Publish_rejects_a_pending_voice()
    {
        var page = api.SeedDraft();
        api.SeedPending(page);

        var response = await api.AsCreatorAsync(HttpMethod.Post, $"/pages/{page.Id.Value:D}/publish");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");
        Assert.Equal("Draft", (await ReadPageAsync(page.Id.Value)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Publish_rejects_a_failed_voice()
    {
        var page = api.SeedDraft();
        var voice = api.SeedPending(page);
        voice.CompletePackagingFailure();

        var response = await api.AsCreatorAsync(HttpMethod.Post, $"/pages/{page.Id.Value:D}/publish");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");
        Assert.Equal("Draft", (await ReadPageAsync(page.Id.Value)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Publish_ready_page_then_a_second_publish_is_a_conflict()
    {
        var page = api.SeedDraft();
        api.SeedReady(page);

        var published = await api.AsCreatorAsync(HttpMethod.Post, $"/pages/{page.Id.Value:D}/publish");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedBody = await ApiJson.ReadAsync(published);
        Assert.Equal("Published", publishedBody.GetProperty("status").GetString());
        Assert.Equal("Ready", publishedBody.GetProperty("voices")[0].GetProperty("packagingStatus").GetString());

        var again = await api.AsCreatorAsync(HttpMethod.Post, $"/pages/{page.Id.Value:D}/publish");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(again), HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
    }

    [Fact]
    public async Task Published_page_rejects_a_pending_voice()
    {
        var page = api.SeedPublished();
        var pending = api.SeedUnattachedPending(page);
        var attached = page.VoiceIds.Count;

        var response = await api.AsCreatorAsync(
            HttpMethod.Post,
            $"/pages/{page.Id.Value:D}/voices/{pending.Id.Value:D}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");
        Assert.Equal(attached, (await ReadPageAsync(page.Id.Value)).GetProperty("voices").GetArrayLength());
    }

    [Fact]
    public async Task Upload_stores_a_pending_voice_and_a_missing_file_does_not()
    {
        var page = api.SeedDraft();
        var puts = api.Blobs.Puts.Count;
        var voices = api.Voices.Count;
        var bytes = new byte[] { 1, 2, 3, 4 };

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Intro"), "title");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "intro.wav");
        using var upload = new HttpRequestMessage(HttpMethod.Post, $"/pages/{page.Id.Value:D}/voices")
        {
            Content = form
        };
        upload.Headers.TryAddWithoutValidation("X-Internal-Key", ContentApiFixture.ApiKey);
        upload.Headers.TryAddWithoutValidation("X-User-Id", api.CreatorId.ToString("D"));
        upload.Headers.TryAddWithoutValidation("X-User-Roles", "ContentCreator");

        var created = await api.Client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ApiJson.ReadAsync(created);
        var voiceId = body.GetProperty("voiceId").GetGuid();
        Assert.Equal($"/voices/{voiceId:D}", created.Headers.Location?.OriginalString);
        Assert.Equal("Pending", body.GetProperty("packagingStatus").GetString());
        Assert.Equal(4, body.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("audio/wav", body.GetProperty("contentType").GetString());
        Assert.Equal(puts + 1, api.Blobs.Puts.Count);

        using var empty = new MultipartFormDataContent();
        empty.Add(new StringContent("No file"), "title");
        using var missingRequest = new HttpRequestMessage(HttpMethod.Post, $"/pages/{page.Id.Value:D}/voices")
        {
            Content = empty
        };
        missingRequest.Headers.TryAddWithoutValidation("X-Internal-Key", ContentApiFixture.ApiKey);
        missingRequest.Headers.TryAddWithoutValidation("X-User-Id", api.CreatorId.ToString("D"));
        missingRequest.Headers.TryAddWithoutValidation("X-User-Roles", "ContentCreator");
        var missing = await api.Client.SendAsync(missingRequest);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(puts + 1, api.Blobs.Puts.Count);
        Assert.Equal(voices + 1, api.Voices.Count);
    }

    [Fact]
    public async Task Reorder_replaces_the_episode_order()
    {
        var page = api.SeedDraft();
        var first = api.SeedPending(page, "First");
        var second = api.SeedPending(page, "Second");

        var response = await api.AsCreatorAsync(
            HttpMethod.Put,
            $"/pages/{page.Id.Value:D}/voices/order",
            new { voiceIds = new[] { second.Id.Value, first.Id.Value } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var voices = (await ApiJson.ReadAsync(response)).GetProperty("voices").EnumerateArray().ToArray();
        Assert.Equal(second.Id.Value, voices[0].GetProperty("voiceId").GetGuid());
        Assert.Equal(first.Id.Value, voices[1].GetProperty("voiceId").GetGuid());
    }

    [Fact]
    public async Task Archive_freezes_the_episode_order_and_the_voice_title()
    {
        var page = api.SeedPublished(voiceCount: 2);
        var voices = (await ReadPageAsync(page.Id.Value)).GetProperty("voices").EnumerateArray().Select(voice => voice.GetProperty("voiceId").GetGuid()).ToArray();

        var archived = await api.AsCreatorAsync(HttpMethod.Post, $"/pages/{page.Id.Value:D}/archive");
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal("Archived", (await ApiJson.ReadAsync(archived)).GetProperty("status").GetString());

        var reorder = await api.AsCreatorAsync(
            HttpMethod.Put,
            $"/pages/{page.Id.Value:D}/voices/order",
            new { voiceIds = new[] { voices[1], voices[0] } });
        Assert.Equal(HttpStatusCode.BadRequest, reorder.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(reorder), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");

        var rename = await api.AsCreatorAsync(
            HttpMethod.Put,
            $"/voices/{voices[0]:D}",
            new { title = "After archive" });
        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
        Assert.Equal("Episode 1", (await ReadVoiceAsync(voices[0])).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Register_waveform_returns_the_bar_layout_and_a_second_revoke_conflicts()
    {
        var page = api.SeedDraft();

        var created = await api.AsCreatorAsync(HttpMethod.Post, "/waveforms", new
        {
            targetType = "Page",
            targetId = page.Id.Value
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ApiJson.ReadAsync(created);
        var waveformId = body.GetProperty("waveformId").GetGuid();
        Assert.Equal($"/waveforms/{waveformId:D}", created.Headers.Location?.OriginalString);
        Assert.Equal(1, body.GetProperty("layoutVersion").GetInt32());
        Assert.True(body.GetProperty("code").GetInt64() > 0);
        var bars = body.GetProperty("bars").EnumerateArray().Select(bar => bar.GetInt32()).ToArray();
        Assert.Equal(24, bars.Length);
        Assert.All(bars, bar => Assert.InRange(bar, 0, 7));

        var revoked = await api.AsCreatorAsync(HttpMethod.Post, $"/waveforms/{waveformId:D}/revoke");
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal("Revoked", (await ApiJson.ReadAsync(revoked)).GetProperty("status").GetString());

        var again = await api.AsCreatorAsync(HttpMethod.Post, $"/waveforms/{waveformId:D}/revoke");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(again), HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
    }

    [Fact]
    public async Task Unknown_page_is_404()
    {
        var response = await api.AsCreatorAsync(HttpMethod.Get, $"/pages/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.NotFound, "https://shenok.dev/problems/not-found");
    }

    private async Task<JsonElement> ReadPageAsync(Guid pageId)
    {
        var response = await api.AsCreatorAsync(HttpMethod.Get, $"/pages/{pageId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiJson.ReadAsync(response);
    }

    private async Task<JsonElement> ReadVoiceAsync(Guid voiceId)
    {
        var response = await api.AsCreatorAsync(HttpMethod.Get, $"/voices/{voiceId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiJson.ReadAsync(response);
    }
}
