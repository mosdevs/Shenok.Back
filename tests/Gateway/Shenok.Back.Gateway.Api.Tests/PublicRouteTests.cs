using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api.Security;

namespace Shenok.Back.Gateway.Api.Tests;

[Collection("Gateway API")]
public sealed class PublicRouteTests(GatewayApiFixture api)
{
    [Fact]
    public async Task Scan_without_a_token_is_401_and_does_not_call_downstream()
    {
        var content = api.Content.Calls;
        var engagement = api.Engagement.Calls;

        using var response = await api.SendAsync(HttpMethod.Post, "/scans", content: Scan("scan-1", [0xFF, 0xD8, 0xFF]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(content, api.Content.Calls);
        Assert.Equal(engagement, api.Engagement.Calls);
    }

    [Fact]
    public async Task Unverified_scan_is_403()
    {
        var token = TestTokens.Create(Guid.NewGuid(), verified: "false");

        using var response = await api.SendAsync(HttpMethod.Post, "/scans", bearer: token, content: Scan("scan-1", [1]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("The caller is not verified.", (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Real_image_is_a_miss_and_does_not_unlock()
    {
        var catalog = new Catalog();
        var content = api.Content.Calls;
        var engagement = api.Engagement.Calls;
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/scans",
                bearer: token,
                content: Scan("scan-1", [0xFF, 0xD8, 0xFF]));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("No waveform matched.", (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
            Assert.Equal(content, api.Content.Calls);
            Assert.Equal(engagement, api.Engagement.Calls);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Page_scan_returns_the_first_voice_and_a_playlist_url_without_following()
    {
        var catalog = new Catalog();
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var engagementBefore = api.Engagement.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/scans",
                bearer: token,
                correlationId: "corr-scan",
                content: Scan("scan-7", Encoding.UTF8.GetBytes($"fixture:page:{catalog.Waveform:D}:{catalog.Page:D}")));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("corr-scan", response.Headers.GetValues("X-Correlation-Id").Single());
            var body = await ApiJson.ReadAsync(response);
            Assert.Equal(catalog.First, body.GetProperty("voice").GetProperty("id").GetGuid());
            Assert.Equal("One", body.GetProperty("voice").GetProperty("title").GetString());
            Assert.Equal(180.5, body.GetProperty("voice").GetProperty("duration").GetDouble());
            Assert.Equal(catalog.Page, body.GetProperty("page").GetProperty("id").GetGuid());
            Assert.Equal("Show", body.GetProperty("page").GetProperty("title").GetString());
            Assert.True(body.GetProperty("promptFollow").GetBoolean());
            Assert.Equal(15, body.GetProperty("resumePositionSeconds").GetInt32());
            Assert.False(body.TryGetProperty("userId", out _));

            var audioUrl = body.GetProperty("voice").GetProperty("audioUrl").GetString();
            Assert.NotNull(audioUrl);
            Assert.StartsWith($"http://127.0.0.1:5080/audio/{catalog.First:D}/playlist.m3u8?", audioUrl, StringComparison.Ordinal);

            var scans = api.Engagement.History.Skip(engagementBefore).ToArray();
            var scan = Assert.Single(scans);
            Assert.Equal(HttpMethod.Post, scan.Method);
            Assert.EndsWith("/internal/scans", scan.Target, StringComparison.Ordinal);
            Assert.Contains("scan-7", scan.Body, StringComparison.Ordinal);
            Assert.Contains(catalog.First.ToString("D"), scan.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(catalog.Listener.ToString("D"), scan.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("/follow", scan.Target, StringComparison.Ordinal);
            Assert.Equal(catalog.Listener.ToString("D"), Assert.Single(api.Engagement.Headers[InternalCaller.UserIdHeader]));

            using var playlist = await api.SendAsync(HttpMethod.Get, new Uri(audioUrl).PathAndQuery);
            Assert.Equal(HttpStatusCode.OK, playlist.StatusCode);
            Assert.Equal("application/vnd.apple.mpegurl", playlist.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store", playlist.Headers.CacheControl?.ToString());
            Assert.Contains("#EXTM3U", await playlist.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var broken = new Uri(audioUrl).PathAndQuery.Replace("sig=", "sig=00", StringComparison.Ordinal);
            var contentBefore = api.Content.Calls;
            using var rejected = await api.SendAsync(HttpMethod.Get, broken);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
            Assert.Equal("The playlist link is not valid.", (await ApiJson.ReadAsync(rejected)).GetProperty("detail").GetString());
            Assert.Equal(contentBefore, api.Content.Calls);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Playlist_rejection_does_not_unlock()
    {
        var catalog = new Catalog { FirstStatus = "Pending" };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var engagement = api.Engagement.Calls;
            var contentBefore = api.Content.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/scans",
                bearer: token,
                content: Scan("scan-pending", Encoding.UTF8.GetBytes($"fixture:page:{catalog.Waveform:D}:{catalog.Page:D}")));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(
                "Voice cannot be played while packaging is Pending.",
                (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
            Assert.Contains(
                api.Content.History.Skip(contentBefore),
                call => call.Target.Contains("/internal/playback", StringComparison.Ordinal));
            Assert.Equal(engagement, api.Engagement.Calls);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Archived_page_does_not_unlock()
    {
        var catalog = new Catalog { PageStatus = "Archived" };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var engagement = api.Engagement.Calls;
            var contentBefore = api.Content.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/scans",
                bearer: token,
                content: Scan("scan-archived", Encoding.UTF8.GetBytes($"fixture:page:{catalog.Waveform:D}:{catalog.Page:D}")));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(
                "Archived pages are hidden from new scans.",
                (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
            Assert.DoesNotContain(
                api.Content.History.Skip(contentBefore),
                call => call.Target.Contains("/internal/playback", StringComparison.Ordinal));
            Assert.Equal(engagement, api.Engagement.Calls);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Missing_content_page_does_not_register_a_scan()
    {
        var catalog = new Catalog { MissingPage = true };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var engagement = api.Engagement.Calls;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/scans",
                bearer: token,
                content: Scan("scan-missing", Encoding.UTF8.GetBytes($"fixture:page:{catalog.Waveform:D}:{catalog.Page:D}")));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await ApiJson.ReadAsync(response);
            Assert.Equal("Page was not found.", body.GetProperty("detail").GetString());
            Assert.NotEqual("downstream", body.GetProperty("traceId").GetString());
            Assert.Equal(engagement, api.Engagement.Calls);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Repeating_a_scan_issues_a_playlist_again_and_does_not_follow()
    {
        var catalog = new Catalog();
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var image = Encoding.UTF8.GetBytes($"fixture:page:{catalog.Waveform:D}:{catalog.Page:D}");
            var before = api.Engagement.History.Count;
            var contentBefore = api.Content.History.Count;

            using var first = await api.SendAsync(HttpMethod.Post, "/scans", bearer: token, content: Scan("same-scan", image));
            using var second = await api.SendAsync(HttpMethod.Post, "/scans", bearer: token, content: Scan("same-scan", image));

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            var calls = api.Engagement.History.Skip(before).ToArray();
            Assert.Equal(2, calls.Length);
            Assert.All(calls, call =>
            {
                Assert.EndsWith("/internal/scans", call.Target, StringComparison.Ordinal);
                Assert.Contains("same-scan", call.Body, StringComparison.Ordinal);
                Assert.DoesNotContain("/follow", call.Target, StringComparison.Ordinal);
            });
            var playlists = api.Content.History
                .Skip(contentBefore)
                .Where(call => call.Target.Contains("/internal/playback", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(2, playlists.Length);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Gif_scan_is_400_and_does_not_call_downstream()
    {
        var content = api.Content.Calls;
        var token = TestTokens.Create(Guid.NewGuid(), roles: ["Listener"]);

        using var response = await api.SendAsync(
            HttpMethod.Post,
            "/scans",
            bearer: token,
            content: Scan("scan-1", [1], "image/gif"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(content, api.Content.Calls);
    }

    [Fact]
    public async Task Locked_listener_cannot_read_the_page_title()
    {
        var catalog = new Catalog { Unlocked = false };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);

            using var response = await api.SendAsync(HttpMethod.Get, $"/pages/{catalog.Page:D}", bearer: token);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            Assert.Contains("The page is locked.", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("Show", raw, StringComparison.Ordinal);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Owner_page_view_merges_counts_and_listening_state()
    {
        var catalog = new Catalog { Unlocked = false, Following = true };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Owner, roles: ["ContentCreator"]);

            using var response = await api.SendAsync(HttpMethod.Get, $"/pages/{catalog.Page:D}", bearer: token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await ApiJson.ReadAsync(response);
            var page = body.GetProperty("page");
            Assert.Equal(catalog.Page, page.GetProperty("id").GetGuid());
            Assert.Equal("Show", page.GetProperty("title").GetString());
            Assert.Equal("https://cdn.example/cover.jpg", page.GetProperty("coverUrl").GetString());
            Assert.Equal(42, page.GetProperty("subscriberCount").GetInt64());
            Assert.False(page.TryGetProperty("ownerId", out _));
            Assert.True(body.GetProperty("isFollowing").GetBoolean());
            var voices = body.GetProperty("voices");
            Assert.Equal(2, voices.GetArrayLength());
            Assert.Equal(catalog.First, voices[0].GetProperty("id").GetGuid());
            Assert.Equal(180.5, voices[0].GetProperty("duration").GetDouble());
            Assert.True(voices[0].GetProperty("isCompleted").GetBoolean());
            Assert.Equal(180, voices[0].GetProperty("positionSeconds").GetInt32());
            Assert.False(voices[0].TryGetProperty("playCount", out _));
            Assert.False(voices[0].TryGetProperty("audioUrl", out _));
            Assert.Equal(catalog.Second, voices[1].GetProperty("id").GetGuid());
            Assert.Equal(0, voices[1].GetProperty("positionSeconds").GetInt32());
            Assert.False(voices[1].GetProperty("isCompleted").GetBoolean());
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Follow_and_unfollow_proxy_engagement_and_do_not_call_content()
    {
        var catalog = new Catalog();
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var content = api.Content.Calls;

            using var follow = await api.SendAsync(HttpMethod.Post, $"/pages/{catalog.Page:D}/follow", bearer: token);
            using var unfollow = await api.SendAsync(HttpMethod.Delete, $"/pages/{catalog.Page:D}/follow", bearer: token);

            Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
            Assert.Equal(HttpStatusCode.OK, unfollow.StatusCode);
            Assert.Equal(content, api.Content.Calls);
            Assert.EndsWith($"/internal/pages/{catalog.Page:D}/follow", api.Engagement.RequestUri?.AbsolutePath, StringComparison.Ordinal);
            Assert.Equal(catalog.Listener.ToString("D"), Assert.Single(api.Engagement.Headers[InternalCaller.UserIdHeader]));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Locked_progress_does_not_reach_engagement_progress()
    {
        var catalog = new Catalog { CanPlay = false };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var before = api.Engagement.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                $"/voices/{catalog.First:D}/progress",
                bearer: token,
                content: JsonContent.Create(new { playSessionId = "session-1", positionSeconds = 4, completed = false, isSeek = true }));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("The page is locked.", (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
            Assert.DoesNotContain(
                api.Engagement.History.Skip(before),
                call => call.Target.Contains("/internal/progress", StringComparison.Ordinal));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Unlocked_progress_is_forwarded_without_a_user_id_field()
    {
        var catalog = new Catalog { CanPlay = true };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var before = api.Engagement.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                $"/voices/{catalog.First:D}/progress",
                bearer: token,
                content: JsonContent.Create(new { playSessionId = " session-9 ", positionSeconds = 4, completed = true, isSeek = true, userId = catalog.Listener }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var progress = api.Engagement.History.Skip(before).Single(call => call.Target.EndsWith("/internal/progress", StringComparison.Ordinal));
            Assert.Contains("session-9", progress.Body, StringComparison.Ordinal);
            Assert.Contains("\"isSeek\":true", progress.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(catalog.Listener.ToString("D"), progress.Body, StringComparison.Ordinal);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Owner_progress_does_not_ask_whether_the_page_is_unlocked()
    {
        var catalog = new Catalog { CanPlay = false };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Owner, roles: ["ContentCreator"]);
            var before = api.Engagement.History.Count;

            using var response = await api.SendAsync(
                HttpMethod.Post,
                $"/voices/{catalog.First:D}/progress",
                bearer: token,
                content: JsonContent.Create(new { playSessionId = "owner-session", positionSeconds = 1, completed = false, isSeek = false }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var calls = api.Engagement.History.Skip(before).ToArray();
            Assert.DoesNotContain(calls, call => call.Target.Contains("/internal/can-play", StringComparison.Ordinal));
            Assert.Contains(calls, call => call.Target.EndsWith("/internal/progress", StringComparison.Ordinal));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Negative_progress_is_400()
    {
        var token = TestTokens.Create(Guid.NewGuid(), roles: ["Listener"]);
        var engagement = api.Engagement.Calls;

        using var response = await api.SendAsync(
            HttpMethod.Post,
            $"/voices/{Guid.NewGuid():D}/progress",
            bearer: token,
            content: JsonContent.Create(new { playSessionId = "session-1", positionSeconds = -1, completed = false, isSeek = false }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(engagement, api.Engagement.Calls);
    }

    private void Clear()
    {
        api.Content.Handle = null;
        api.Engagement.Handle = null;
        api.Identity.Handle = null;
    }

    private static HttpContent Scan(string scanId, byte[] image, string contentType = "image/jpeg")
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(scanId), "scanId");
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "image", "wave.jpg");
        return form;
    }

    private sealed class Catalog
    {
        public Guid Owner { get; } = Guid.NewGuid();

        public Guid Listener { get; } = Guid.NewGuid();

        public Guid Page { get; } = Guid.NewGuid();

        public Guid First { get; } = Guid.NewGuid();

        public Guid Second { get; } = Guid.NewGuid();

        public Guid Waveform { get; } = Guid.NewGuid();

        public bool Unlocked { get; set; } = true;

        public bool Following { get; set; }

        public bool CanPlay { get; set; }

        public bool MissingPage { get; set; }

        public string FirstStatus { get; set; } = "Ready";

        public string PageStatus { get; set; } = "Published";

        public void Attach(GatewayApiFixture api)
        {
            api.Content.Handle = Content;
            api.Engagement.Handle = Engagement;
        }

        private HttpResponseMessage Content(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("/internal/playback", StringComparison.Ordinal))
            {
                if (!string.Equals(FirstStatus, "Ready", StringComparison.Ordinal))
                    return Problem(HttpStatusCode.Conflict, "Voice cannot be played while packaging is Pending.");

                return Json($$"""
                    {
                      "voiceId": "{{First:D}}",
                      "pageId": "{{Page:D}}",
                      "title": "One",
                      "durationSeconds": 180.5,
                      "playlist": "#EXTM3U\nseg.ts\n",
                      "expiresUnixSeconds": 4102444800
                    }
                    """);
            }

            if (path.Contains("/internal/voices/", StringComparison.Ordinal))
            {
                return Json($$"""
                    {
                      "voiceId": "{{First:D}}",
                      "pageId": "{{Page:D}}",
                      "title": "One",
                      "durationSeconds": 180.5,
                      "packagingStatus": "{{FirstStatus}}"
                    }
                    """);
            }

            if (MissingPage)
                return Problem(HttpStatusCode.NotFound, "Page was not found.");

            return Json($$"""
                {
                  "pageId": "{{Page:D}}",
                  "ownerId": "{{Owner:D}}",
                  "title": "Show",
                  "description": "A show",
                  "coverImageUrl": "https://cdn.example/cover.jpg",
                  "status": "{{PageStatus}}",
                  "createdAt": "2026-11-09T00:00:00Z",
                  "subscriberCount": 42,
                  "voices": [
                    {
                      "voiceId": "{{First:D}}",
                      "title": "One",
                      "durationSeconds": 180.5,
                      "packagingStatus": "{{FirstStatus}}",
                      "playCount": 9
                    },
                    {
                      "voiceId": "{{Second:D}}",
                      "title": "Two",
                      "durationSeconds": 20,
                      "packagingStatus": "Pending",
                      "playCount": 0
                    }
                  ]
                }
                """);
        }

        private HttpResponseMessage Engagement(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/internal/scans", StringComparison.Ordinal))
            {
                return Json("""
                    {"unlocked":true,"isFollowing":false,"promptFollow":true,"resumePositionSeconds":15}
                    """);
            }

            if (path.EndsWith("/internal/listening-state", StringComparison.Ordinal))
            {
                return Json($$"""
                    {
                      "isFollowing": {{(Following ? "true" : "false")}},
                      "unlocked": {{(Unlocked ? "true" : "false")}},
                      "voices": [
                        {"voiceId": "{{First:D}}", "isCompleted": true, "positionSeconds": 180}
                      ]
                    }
                    """);
            }

            if (path.EndsWith("/internal/can-play", StringComparison.Ordinal))
                return Json($$"""{"canPlay": {{(CanPlay ? "true" : "false")}}}""");

            if (path.EndsWith("/follow", StringComparison.Ordinal) || path.EndsWith("/internal/progress", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK);

            return Problem(HttpStatusCode.NotFound, "The resource was not found.");
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

        private static HttpResponseMessage Problem(HttpStatusCode status, string detail) =>
            new(status)
            {
                Content = new StringContent(
                    $$"""{"type":"https://shenok.dev/problems/not-found","title":"Not Found","status":{{(int)status}},"detail":"{{detail}}","traceId":"downstream"}""",
                    Encoding.UTF8,
                    "application/problem+json")
            };
    }
}
