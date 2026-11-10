using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Api.Playback;
using Shenok.Back.Gateway.Application.Common;

namespace Shenok.Back.Gateway.Api.Tests;

[Collection("Gateway API")]
public sealed class IdentityProxyTests(GatewayApiFixture api)
{
    [Theory]
    [InlineData("/register", "{\"username\":\"ada\",\"displayName\":\"Ada\",\"email\":\"ada@example.com\",\"password\":\"secret\"}", "ada")]
    [InlineData("/otp/request", "{\"phoneNumber\":\"+15551212\",\"purpose\":\"register\"}", "15551212")]
    [InlineData("/otp/confirm", "{\"phoneNumber\":\"+15551212\",\"code\":\"123456\",\"purpose\":\"register\"}", "123456")]
    [InlineData("/login", "{\"email\":\"ada@example.com\",\"password\":\"secret\"}", "ada@example.com")]
    public async Task Open_routes_forward_without_a_token(string path, string json, string marker)
    {
        var before = api.Identity.Calls;
        try
        {
            api.Identity.Handle = _ => Json(HttpStatusCode.OK, "{\"ok\":true}");

            using var response = await api.SendAsync(
                HttpMethod.Post,
                path,
                content: new StringContent(json, Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"ok\":true}", await response.Content.ReadAsStringAsync());
            Assert.Equal(before + 1, api.Identity.Calls);
            var call = api.Identity.History[^1];
            Assert.Equal(path, call.Target);
            Assert.Contains(marker, call.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("userId", call.Body, StringComparison.OrdinalIgnoreCase);
            Assert.False(api.Identity.Headers.ContainsKey(InternalCaller.UserIdHeader));
        }
        finally
        {
            api.Identity.Handle = null;
        }
    }

    [Fact]
    public async Task Downstream_problem_status_and_body_are_preserved()
    {
        const string problem =
            """{"type":"https://shenok.dev/problems/conflict","title":"Conflict","status":409,"detail":"Email is already registered.","traceId":"downstream"}""";
        try
        {
            api.Identity.Handle = _ => Json(HttpStatusCode.Conflict, problem, "application/problem+json");

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/register",
                content: new StringContent("{\"username\":\"ada\"}", Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(problem, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            api.Identity.Handle = null;
        }
    }

    [Fact]
    public async Task Refresh_without_a_token_is_401_and_is_not_forwarded()
    {
        var before = api.Identity.Calls;

        using var response = await api.SendAsync(
            HttpMethod.Post,
            "/refresh",
            content: JsonContent.Create(new { refreshToken = "stored-token" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, api.Identity.Calls);
    }

    [Fact]
    public async Task Unverified_refresh_is_403_and_is_not_forwarded()
    {
        var before = api.Identity.Calls;
        var token = TestTokens.Create(Guid.NewGuid(), verified: "false");

        using var response = await api.SendAsync(
            HttpMethod.Post,
            "/refresh",
            bearer: token,
            content: JsonContent.Create(new { refreshToken = "stored-token" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("The caller is not verified.", (await ApiJson.ReadAsync(response)).GetProperty("detail").GetString());
        Assert.Equal(before, api.Identity.Calls);
    }

    [Fact]
    public async Task Refresh_forwards_the_body_and_keeps_the_user_id_out_of_it()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(userId, roles: ["Listener"]);
        try
        {
            api.Identity.Handle = _ => Json(HttpStatusCode.OK, "{\"accessToken\":\"next\"}");

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/refresh",
                bearer: token,
                content: JsonContent.Create(new { refreshToken = "stored-token", userId }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var call = api.Identity.History[^1];
            Assert.Equal("/refresh", call.Target);
            Assert.Contains("stored-token", call.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(userId.ToString("D"), call.Body, StringComparison.Ordinal);
            Assert.Equal(userId.ToString("D"), Assert.Single(api.Identity.Headers[InternalCaller.UserIdHeader]));
        }
        finally
        {
            api.Identity.Handle = null;
        }
    }

    [Fact]
    public async Task Logout_and_profile_routes_are_forwarded_for_a_verified_caller()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(userId, roles: ["Listener"]);
        try
        {
            api.Identity.Handle = _ => Json(HttpStatusCode.OK, "{\"ok\":true}");

            using var logout = await api.SendAsync(
                HttpMethod.Post,
                "/logout",
                bearer: token,
                content: JsonContent.Create(new { refreshToken = "stored-token" }));
            using var me = await api.SendAsync(HttpMethod.Get, "/users/me", bearer: token);
            using var update = await api.SendAsync(
                HttpMethod.Patch,
                "/users/me",
                bearer: token,
                content: JsonContent.Create(new { username = "ada", displayName = "Ada", avatarUrl = (string?)null, bio = "hi" }));
            using var password = await api.SendAsync(
                HttpMethod.Post,
                "/users/me/password",
                bearer: token,
                content: JsonContent.Create(new { currentPassword = "old", newPassword = "new-secret", userId }));
            using var profile = await api.SendAsync(HttpMethod.Get, "/users/ada", bearer: token);

            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal(HttpStatusCode.OK, password.StatusCode);
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            Assert.Contains(api.Identity.History, call => call.Target == "/logout" && call.Body is not null && call.Body.Contains("stored-token", StringComparison.Ordinal));
            Assert.Contains(api.Identity.History, call => call.Target == "/users/me" && call.Method == HttpMethod.Get);
            Assert.Contains(api.Identity.History, call => call.Target == "/users/ada");
            var passwordCall = api.Identity.History.Last(call => call.Target == "/users/me/password");
            Assert.Contains("new-secret", passwordCall.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(userId.ToString("D"), passwordCall.Body, StringComparison.Ordinal);
        }
        finally
        {
            api.Identity.Handle = null;
        }
    }

    [Fact]
    public async Task Internal_role_grant_is_not_proxied()
    {
        var before = api.Identity.Calls;
        var token = TestTokens.Create(Guid.NewGuid(), roles: ["Admin"]);

        using var response = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/users/{Guid.NewGuid():D}/roles",
            bearer: token,
            content: JsonContent.Create(new { role = "ContentCreator" }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, api.Identity.Calls);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        };
}

[Collection("Gateway API")]
public sealed class CreatorAndMediaTests(GatewayApiFixture api)
{
    [Fact]
    public async Task Owner_playback_omits_the_waveform_and_a_stranger_does_not_issue_one()
    {
        var catalog = new CatalogDouble();
        try
        {
            catalog.Attach(api);
            var owner = TestTokens.Create(catalog.Owner, roles: ["ContentCreator"]);
            var stranger = TestTokens.Create(catalog.Listener, roles: ["Listener"]);
            var before = api.Content.History.Count;

            using var allowed = await api.SendAsync(HttpMethod.Post, $"/voices/{catalog.Voice:D}/playback", bearer: owner);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            var body = await ApiJson.ReadAsync(allowed);
            var audioUrl = body.GetProperty("audioUrl").GetString();
            Assert.NotNull(audioUrl);
            Assert.Contains("/playlist.m3u8?exp=", audioUrl, StringComparison.Ordinal);
            Assert.DoesNotContain("waveformId", audioUrl, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(180.5, body.GetProperty("duration").GetDouble());
            Assert.Equal(4_102_444_800, body.GetProperty("expiresUnixSeconds").GetInt64());

            var issued = api.Content.History.Skip(before).Single(call => call.Target.Contains("/internal/playback", StringComparison.Ordinal));
            Assert.DoesNotContain("waveformId", issued.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                api.Engagement.History,
                call => call.Target.Contains("/internal/can-play", StringComparison.Ordinal)
                    && call.Body is not null
                    && call.Body.Contains(catalog.Page.ToString("D"), StringComparison.Ordinal));

            var playbacks = api.Content.History.Count(call => call.Target.Contains("/internal/playback", StringComparison.Ordinal));
            using var rejected = await api.SendAsync(HttpMethod.Post, $"/voices/{catalog.Voice:D}/playback", bearer: stranger);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
            Assert.Equal(playbacks, api.Content.History.Count(call => call.Target.Contains("/internal/playback", StringComparison.Ordinal)));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Unlocked_listener_can_start_playback()
    {
        var catalog = new CatalogDouble { CanPlay = true };
        try
        {
            catalog.Attach(api);
            var token = TestTokens.Create(catalog.Listener, roles: ["Listener"]);

            using var response = await api.SendAsync(HttpMethod.Post, $"/voices/{catalog.Voice:D}/playback", bearer: token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(api.Engagement.History, call => call.Target.Contains("/internal/can-play", StringComparison.Ordinal));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Playlist_and_segment_reject_a_bad_or_expired_signature_without_calling_content()
    {
        var voiceId = Guid.NewGuid();
        var future = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var playlist = PlaybackPaths.Playlist(string.Empty, voiceId);
        var segment = PlaybackPaths.Segment(string.Empty, voiceId, "seg_00000.ts");
        var playlistSignature = PlaybackSignature.Sign(GatewayTestSettings.PlaybackKey, playlist, future);
        var segmentSignature = PlaybackSignature.Sign(GatewayTestSettings.PlaybackKey, segment, future);

        var before = api.Content.Calls;
        using var badPlaylist = await api.SendAsync(HttpMethod.Get, $"{playlist}?exp={future}&sig=00");
        using var expiredPlaylist = await api.SendAsync(HttpMethod.Get, $"{playlist}?exp=1700000000&sig={playlistSignature}");
        using var badSegment = await api.SendAsync(HttpMethod.Get, $"{segment}?exp={future}&sig=00");
        using var expiredSegment = await api.SendAsync(HttpMethod.Get, $"{segment}?exp=1700000000&sig={segmentSignature}");

        Assert.Equal(HttpStatusCode.Forbidden, badPlaylist.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, expiredPlaylist.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, badSegment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, expiredSegment.StatusCode);
        Assert.Equal(before, api.Content.Calls);
    }

    [Fact]
    public async Task Valid_segment_signature_calls_content_once_and_streams_the_bytes()
    {
        var voiceId = Guid.NewGuid();
        var future = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var segment = PlaybackPaths.Segment(string.Empty, voiceId, "seg_00000.ts");
        var signature = PlaybackSignature.Sign(GatewayTestSettings.PlaybackKey, segment, future);
        var before = api.Content.History.Count;
        try
        {
            api.Content.Handle = request =>
            {
                var message = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp2t");
                return message;
            };

            using var response = await api.SendAsync(HttpMethod.Get, $"{segment}?exp={future}&sig={signature}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("video/mp2t", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await response.Content.ReadAsByteArrayAsync());
            var calls = api.Content.History.Skip(before).ToArray();
            var call = Assert.Single(calls);
            Assert.Contains($"/internal/voices/{voiceId:D}/segments/seg_00000.ts", call.Target, StringComparison.Ordinal);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Direct_playlist_refreshes_segments_without_a_waveform()
    {
        var voiceId = Guid.NewGuid();
        var future = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var playlist = PlaybackPaths.Playlist(string.Empty, voiceId);
        var signature = PlaybackSignature.Sign(GatewayTestSettings.PlaybackKey, playlist, future);
        var before = api.Content.History.Count;
        try
        {
            api.Content.Handle = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"playlist":"#EXTM3U\nseg.ts\n","expiresUnixSeconds":4102444800}""",
                    Encoding.UTF8,
                    "application/json")
            };

            using var response = await api.SendAsync(HttpMethod.Get, $"{playlist}?exp={future}&sig={signature}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/vnd.apple.mpegurl", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains("#EXTM3U", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            var call = api.Content.History.Skip(before).Single();
            Assert.Contains("/internal/playback", call.Target, StringComparison.Ordinal);
            Assert.DoesNotContain("waveformId", call.Body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Waveform_register_response_is_passed_through()
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(userId, roles: ["ContentCreator"]);
        const string body =
            """{"waveformId":"33333333-3333-3333-3333-333333333333","code":7,"layoutVersion":1,"targetType":"Page","targetId":"44444444-4444-4444-4444-444444444444","bars":[1,2,3]}""";
        try
        {
            api.Content.Handle = request =>
            {
                var message = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                message.Headers.Location = new Uri("http://127.0.0.1:5117/waveforms/33333333-3333-3333-3333-333333333333");
                return message;
            };

            using var response = await api.SendAsync(
                HttpMethod.Post,
                "/waveforms",
                bearer: token,
                content: new StringContent(
                    """{"targetType":"Page","targetId":"44444444-4444-4444-4444-444444444444"}""",
                    Encoding.UTF8,
                    "application/json"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("/waveforms/33333333-3333-3333-3333-333333333333", LocationPath(response));
            Assert.Equal(body, await response.Content.ReadAsStringAsync());
            Assert.Equal(userId.ToString("D"), Assert.Single(api.Content.Headers[InternalCaller.UserIdHeader]));
            Assert.Equal("ContentCreator", Assert.Single(api.Content.Headers[InternalCaller.RolesHeader]));
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Small_upload_is_streamed_to_content()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var token = TestTokens.Create(userId, roles: ["ContentCreator"]);
        try
        {
            api.Content.Handle = _ =>
            {
                var message = new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"voiceId\":\"55555555-5555-5555-5555-555555555555\"}", Encoding.UTF8, "application/json")
                };
                message.Headers.Location = new Uri("http://127.0.0.1:5117/voices/55555555-5555-5555-5555-555555555555");
                return message;
            };

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("Episode"), "Title");
            var file = new ByteArrayContent([1, 2, 3]);
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
            form.Add(file, "File", "episode.mp3");

            using var response = await api.SendAsync(
                HttpMethod.Post,
                $"/pages/{pageId:D}/voices",
                bearer: token,
                content: form);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("/voices/55555555-5555-5555-5555-555555555555", LocationPath(response));
            var call = api.Content.History[^1];
            Assert.Contains($"/pages/{pageId:D}/voices", call.Target, StringComparison.Ordinal);
            Assert.Contains("Episode", call.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(userId.ToString("D"), call.Body, StringComparison.Ordinal);
        }
        finally
        {
            Clear();
        }
    }

    [Fact]
    public async Task Upload_over_the_size_limit_does_not_call_content()
    {
        var token = TestTokens.Create(Guid.NewGuid(), roles: ["ContentCreator"]);
        var before = api.Content.Calls;
        var upload = new BoundedReadStream();
        using var body = new StreamContent(upload);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=----shenok");
        body.Headers.ContentLength = GatewayLimits.MaxUploadRequestBytes + 1;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/pages/{Guid.NewGuid():D}/voices")
        {
            Content = body
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Kestrel rejects Content-Length before it writes 100-continue, so the body is not uploaded.
        request.Headers.ExpectContinue = true;

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(before, api.Content.Calls);
        Assert.Equal(0, upload.BytesRead);
    }

    private static string? LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
            return null;

        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private void Clear()
    {
        api.Content.Handle = null;
        api.Engagement.Handle = null;
        api.Identity.Handle = null;
    }

    private sealed class BoundedReadStream : Stream
    {
        private int _read;

        public int BytesRead => _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => GatewayLimits.MaxUploadRequestBytes + 1;

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Length));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Read(int count)
        {
            _read += count;
            throw new InvalidOperationException("The oversized upload was read.");
        }
    }

    private sealed class CatalogDouble
    {
        public Guid Owner { get; } = Guid.NewGuid();

        public Guid Listener { get; } = Guid.NewGuid();

        public Guid Page { get; } = Guid.NewGuid();

        public Guid Voice { get; } = Guid.NewGuid();

        public bool CanPlay { get; set; }

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
                return Json($$"""
                    {"playlist":"#EXTM3U\n","expiresUnixSeconds":4102444800}
                    """);
            }

            if (path.Contains("/internal/voices/", StringComparison.Ordinal))
            {
                return Json($$"""
                    {"voiceId":"{{Voice:D}}","pageId":"{{Page:D}}","title":"One","durationSeconds":180.5,"packagingStatus":"Ready"}
                    """);
            }

            return Json($$"""
                {"pageId":"{{Page:D}}","ownerId":"{{Owner:D}}","title":"Show","coverImageUrl":null,"status":"Published","subscriberCount":1,"voices":[]}
                """);
        }

        private HttpResponseMessage Engagement(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/internal/can-play", StringComparison.Ordinal))
                return Json($$"""{"canPlay":{{(CanPlay ? "true" : "false")}}}""");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
