using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

/// <summary>
/// Channel-style aggregate. <see cref="VoiceIds"/> is the only episode order.
/// Subscriber count lives on <c>PageCounter</c> and is never changed here.
/// <see cref="EnsureOwnedBy"/> enforces the owner. Admin bypass lives in the application use case, which skips that call for an Admin role. This type does not read roles.
/// </summary>
public sealed class Page : AggregateRoot<PageId>
{
    private List<VoiceId> _voiceIds = [];

    public PageTitle Title { get; private set; } = null!;
    public PageDescription Description { get; private set; } = null!;
    public CoverImageUrl? CoverImageUrl { get; private set; }
    public OwnerId OwnerId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public PageStatus Status { get; private set; }
    public IReadOnlyList<VoiceId> VoiceIds => _voiceIds;

    private Page()
    {
    }

    private Page(
        PageId id,
        OwnerId ownerId,
        PageTitle title,
        PageDescription description,
        CoverImageUrl? coverImageUrl,
        DateTime createdAt) : base(id)
    {
        OwnerId = ownerId;
        Title = title;
        Description = description;
        CoverImageUrl = coverImageUrl;
        CreatedAt = createdAt;
        Status = PageStatus.Draft;
    }

    public static Page CreateDraft(
        OwnerId ownerId,
        PageTitle title,
        PageDescription description,
        CoverImageUrl? coverImageUrl = null)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        var page = new Page(PageId.New(), ownerId, title, description, coverImageUrl, DateTime.UtcNow);
        page.RaiseDomainEvent(new PageCreatedDomainEvent(page.Id, page.OwnerId));
        return page;
    }

    public void EnsureOwnedBy(OwnerId actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (OwnerId != actor)
            throw new NotPageOwnerException(Id, actor);
    }

    public void UpdateMetadata(PageTitle title, PageDescription description, CoverImageUrl? coverImageUrl)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);

        Title = title;
        Description = description;
        CoverImageUrl = coverImageUrl;
    }

    /// <summary>
    /// Appends a voice to the episode order.
    /// Draft pages may attach <see cref="PackagingStatus.Pending"/> or <see cref="PackagingStatus.Failed"/> voices.
    /// Published pages may attach only <see cref="PackagingStatus.Ready"/> voices so a live page cannot pick up a non-ready episode.
    /// The application loads the voice and passes its current packaging status.
    /// </summary>
    public void AddVoiceReference(VoiceId voiceId, PackagingStatus packagingStatus)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(voiceId);
        if (!Enum.IsDefined(packagingStatus))
            throw new ArgumentOutOfRangeException(nameof(packagingStatus), packagingStatus, "Packaging status is not defined.");
        if (_voiceIds.Contains(voiceId))
            throw new DuplicateVoiceOnPageException(Id, voiceId);

        if (Status is PageStatus.Published && packagingStatus is not PackagingStatus.Ready)
            throw new VoiceNotReadyForPublishException(voiceId, packagingStatus);

        _voiceIds.Add(voiceId);
    }

    /// <summary>
    /// Replaces the episode order. The new list must be the same set of voice ids.
    /// Archived pages keep the order they had when they were archived.
    /// </summary>
    public void ReorderVoices(IReadOnlyList<VoiceId> newOrder)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(newOrder);

        if (!IsSameVoiceSet(newOrder))
            throw new InvalidVoiceOrderException();

        _voiceIds.Clear();
        _voiceIds.AddRange(newOrder);
    }

    /// <summary>
    /// Publishes a draft when every referenced voice is <see cref="PackagingStatus.Ready"/>.
    /// The application loads the voices and passes their packaging status. A missing entry is not ready.
    /// Not idempotent: a second call throws <see cref="PageAlreadyPublishedException"/>. Map that to a conflict in the use case.
    /// </summary>
    public void Publish(IReadOnlyDictionary<VoiceId, PackagingStatus> readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        if (Status == PageStatus.Archived)
            throw new CannotPublishArchivedPageException(Id);

        if (Status == PageStatus.Published)
            throw new PageAlreadyPublishedException(Id);

        if (_voiceIds.Count == 0)
            throw new PageHasNoVoicesException(Id);

        foreach (var voiceId in _voiceIds)
        {
            if (!readiness.TryGetValue(voiceId, out var packagingStatus))
                throw new VoiceNotReadyForPublishException(voiceId, null);

            if (packagingStatus != PackagingStatus.Ready)
                throw new VoiceNotReadyForPublishException(voiceId, packagingStatus);
        }

        Status = PageStatus.Published;
        RaiseDomainEvent(new PagePublishedDomainEvent(Id));
    }

    public void Archive()
    {
        if (Status == PageStatus.Archived)
            throw new PageArchivedException(Id);

        Status = PageStatus.Archived;
        RaiseDomainEvent(new PageArchivedDomainEvent(Id));
    }

    private void EnsureNotArchived()
    {
        if (Status == PageStatus.Archived)
            throw new PageArchivedException(Id);
    }

    private bool IsSameVoiceSet(IReadOnlyList<VoiceId> newOrder)
    {
        if (newOrder.Count != _voiceIds.Count)
            return false;

        var seen = new HashSet<VoiceId>();
        foreach (var voiceId in newOrder)
        {
            ArgumentNullException.ThrowIfNull(voiceId);
            if (!_voiceIds.Contains(voiceId) || !seen.Add(voiceId))
                return false;
        }

        return seen.Count == _voiceIds.Count;
    }
}
