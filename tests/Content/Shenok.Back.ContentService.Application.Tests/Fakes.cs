using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;

namespace Shenok.Back.ContentService.Application.Tests;

internal sealed class FakeCaller : IContentCaller
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public bool IsAdmin { get; set; }
    public bool IsContentCreator { get; set; } = true;
    public bool IsInternalService { get; set; }
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }
    public Exception? Failure { get; set; }

    public Task<int> SaveChanges(CancellationToken ct = default)
    {
        if (Failure is not null)
            throw Failure;

        Saves++;
        return Task.FromResult(1);
    }
}

internal sealed class FakePages : IPageRepository
{
    private readonly Dictionary<Guid, Page> _items = [];

    public Task AddAsync(Page page, CancellationToken ct = default)
    {
        _items[page.Id.Value] = page;
        return Task.CompletedTask;
    }

    public Task<Page?> GetByIdAsync(PageId id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id.Value));

    public Task<IReadOnlyList<Page>> ListByOwnerAsync(OwnerId ownerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Page>>(_items.Values.Where(page => page.OwnerId == ownerId).ToArray());

    public Task UpdateAsync(Page page, CancellationToken ct = default)
    {
        _items[page.Id.Value] = page;
        return Task.CompletedTask;
    }
}

internal sealed class FakeVoices : IVoiceRepository
{
    private readonly Dictionary<Guid, Voice> _items = [];

    public int Gets { get; private set; }

    public bool HideAfterFirstGet { get; set; }

    public void Add(Voice voice) => _items[voice.Id.Value] = voice;

    public Task AddAsync(Voice voice, CancellationToken ct = default)
    {
        Add(voice);
        return Task.CompletedTask;
    }

    public Task<Voice?> GetByIdAsync(VoiceId id, CancellationToken ct = default)
    {
        Gets++;
        if (HideAfterFirstGet && Gets > 1)
            return Task.FromResult<Voice?>(null);

        return Task.FromResult(_items.GetValueOrDefault(id.Value));
    }

    public Task<IReadOnlyList<Voice>> ListByPageIdsAsync(IReadOnlyCollection<PageId> pageIds, CancellationToken ct = default)
    {
        var ids = pageIds.Select(id => id.Value).ToHashSet();
        IReadOnlyList<Voice> matches = _items.Values.Where(voice => ids.Contains(voice.PageId.Value)).ToArray();
        return Task.FromResult(matches);
    }

    public Task UpdateAsync(Voice voice, CancellationToken ct = default)
    {
        _items[voice.Id.Value] = voice;
        return Task.CompletedTask;
    }
}

internal sealed class FakeMappings : IWaveformMappingRepository
{
    private readonly Dictionary<Guid, WaveformMapping> _items = [];

    public int Adds { get; private set; }
    public bool RejectNextCode { get; set; }
    public int RejectRemaining { get; set; }

    public Task AddAsync(WaveformMapping mapping, CancellationToken ct = default)
    {
        if (RejectNextCode)
        {
            RejectNextCode = false;
            throw new WaveformCodeAlreadyUsedException();
        }

        if (RejectRemaining > 0)
        {
            RejectRemaining--;
            throw new WaveformCodeAlreadyUsedException();
        }

        Adds++;
        _items[mapping.Id.Value] = mapping;
        return Task.CompletedTask;
    }

    public Task<bool> CodeExistsAsync(WaveformCode code, CancellationToken ct = default) =>
        Task.FromResult(_items.Values.Any(mapping => mapping.WaveformCode == code));

    public Task<WaveformMapping?> GetByCodeAsync(WaveformCode code, CancellationToken ct = default) =>
        Task.FromResult(_items.Values.FirstOrDefault(mapping => mapping.WaveformCode == code));

    public Task<WaveformMapping?> GetByIdAsync(WaveformId id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id.Value));

    public Task<IReadOnlyList<WaveformMapping>> ListByTargetAsync(
        WaveformTargetType targetType,
        Guid targetId,
        CancellationToken ct = default)
    {
        IReadOnlyList<WaveformMapping> matches = _items.Values
            .Where(mapping => mapping.TargetType == targetType && mapping.TargetId == targetId)
            .ToArray();
        return Task.FromResult(matches);
    }

    public Task UpdateAsync(WaveformMapping mapping, CancellationToken ct = default)
    {
        _items[mapping.Id.Value] = mapping;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCodes : IWaveformCodeAllocator
{
    private long _next = 7;

    public int Calls { get; private set; }

    public Task<WaveformCode> AllocateAsync(CancellationToken ct = default)
    {
        Calls++;
        var code = WaveformCode.From(_next);
        _next++;
        return Task.FromResult(code);
    }
}

internal sealed class FakeBlobs : IBlobStorage
{
    public List<string> Puts { get; } = [];
    public List<string> Deletes { get; } = [];
    public int Reads { get; private set; }
    public Exception? DeleteFailure { get; set; }
    public string Playlist { get; set; } = "#EXTM3U\n#EXTINF:1.0,\nseg000.aac\n";

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (DeleteFailure is not null)
            throw DeleteFailure;

        Deletes.Add(storageKey);
        return Task.CompletedTask;
    }

    public Task PutAsync(BlobUpload upload, CancellationToken cancellationToken)
    {
        Puts.Add(upload.StorageKey);
        return Task.CompletedTask;
    }

    public Task<string> ReadTextAsync(string storageKey, int maxChars, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Playlist);
    }
}

internal sealed class FakePackager : IAudioPackager
{
    public int Calls { get; private set; }
    public double SeenMaxDuration { get; private set; }
    public AudioPackageResult Result { get; set; } =
        new AudioPackageResult.Succeeded(12.5, "hls/playlist.m3u8", "hls/packagesegment/");

    public Task<AudioPackageResult> PackageAsync(
        string sourceStorageKey,
        double maxDurationSeconds,
        CancellationToken cancellationToken)
    {
        Calls++;
        SeenMaxDuration = maxDurationSeconds;
        return Task.FromResult(Result);
    }
}

internal sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
