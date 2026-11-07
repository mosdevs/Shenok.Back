using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

/// <summary>
/// Caller for the packaging actor. It is not an HTTP user. PackageVoice accepts only an internal caller.
/// </summary>
internal sealed class PackagingCaller : IContentCaller
{
    public static PackagingCaller Instance { get; } = new();

    private PackagingCaller()
    {
    }

    public Guid UserId => Guid.Empty;

    public bool IsAdmin => false;

    public bool IsContentCreator => false;

    public bool IsInternalService => true;
}
