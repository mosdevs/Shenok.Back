namespace Shenok.Back.ContentService.Application.Common.Interfaces;

/// <summary>
/// Caller taken from trusted gateway headers, never from the request body.
/// <see cref="IsAdmin"/> is the role <c>Admin</c>. <see cref="IsContentCreator"/> is the role <c>ContentCreator</c>.
/// <see cref="IsInternalService"/> is set only for calls that already passed the internal API key.
/// </summary>
public interface IContentCaller
{
    Guid UserId { get; }

    bool IsAdmin { get; }

    bool IsContentCreator { get; }

    bool IsInternalService { get; }
}
