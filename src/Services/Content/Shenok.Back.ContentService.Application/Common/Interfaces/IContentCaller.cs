namespace Shenok.Back.ContentService.Application.Common.Interfaces;

/// <summary>
/// Caller taken from trusted gateway headers, never from the request body.
/// <see cref="IsAdmin"/> is the role <c>Admin</c>. <see cref="IsContentCreator"/> is the role <c>ContentCreator</c>.
/// <see cref="IsInternalService"/> is set only for an endpoint marked as an internal service call, after the API key has been accepted.
/// </summary>
public interface IContentCaller
{
    Guid UserId { get; }

    bool IsAdmin { get; }

    bool IsContentCreator { get; }

    bool IsInternalService { get; }
}
