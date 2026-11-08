using Microsoft.AspNetCore.Http;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Api.Security;

namespace Shenok.Back.ContentService.Api.Tests;

public sealed class HttpContentCallerTests
{
    [Fact]
    public void Creator_route_reads_the_user_and_roles()
    {
        var userId = Guid.NewGuid();
        var caller = Caller("/pages", userId, "ContentCreator, Admin");

        Assert.Equal(userId, caller.UserId);
        Assert.True(caller.IsContentCreator);
        Assert.True(caller.IsAdmin);
        Assert.False(caller.IsInternalService);
    }

    [Fact]
    public void Marked_internal_endpoint_is_a_service_call_without_a_user_id()
    {
        var caller = Caller("/internal/playback", userId: null, roles: null, serviceEndpoint: true);

        Assert.True(caller.IsInternalService);
        Assert.False(caller.IsAdmin);
        Assert.False(caller.IsContentCreator);
        var missing = Assert.Throws<UnauthorizedException>(() => caller.UserId);
        Assert.Equal("The caller id is missing.", missing.Message);
    }

    [Fact]
    public void The_internal_prefix_and_a_client_header_cannot_mark_a_service_call()
    {
        var prefixed = Caller("/internal/playback", userId: null, roles: null);
        Assert.False(prefixed.IsInternalService);

        var http = new DefaultHttpContext();
        http.Request.Path = "/pages";
        http.Request.Headers["X-Internal-Service"] = "true";
        http.Items[InternalCaller.ServiceItemKey] = "true";
        var caller = new HttpContentCaller(new HttpContextAccessor { HttpContext = http });

        Assert.False(caller.IsInternalService);
    }

    [Fact]
    public void A_path_that_only_starts_with_the_word_internal_is_a_creator_route()
    {
        var caller = Caller("/internalism", Guid.NewGuid(), "admin");

        Assert.False(caller.IsInternalService);
        Assert.False(caller.IsAdmin);
    }

    [Fact]
    public void Missing_request_cannot_invent_a_caller()
    {
        var caller = new HttpContentCaller(new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() => caller.IsInternalService);
    }

    private static HttpContentCaller Caller(string path, Guid? userId, string? roles, bool serviceEndpoint = false)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        if (userId is Guid id)
            http.Request.Headers[InternalCaller.UserIdHeader] = id.ToString();
        if (roles is not null)
            http.Request.Headers[InternalCaller.RolesHeader] = roles;
        if (serviceEndpoint)
            http.Items[InternalCaller.ServiceItemKey] = true;

        return new HttpContentCaller(new HttpContextAccessor { HttpContext = http });
    }
}
