using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;

namespace Shenok.Back.ContentService.Api.Tests;

public sealed class MemoryPages : IPageRepository
{
    private readonly Dictionary<Guid, Page> _items = [];

    public int Count => _items.Count;

    public void Add(Page page) => _items[page.Id.Value] = page;

    public Task AddAsync(Page page, CancellationToken ct = default)
    {
        Add(page);
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

public sealed class MemoryVoices : IVoiceRepository
{
    private readonly Dictionary<Guid, Voice> _items = [];

    public int Count => _items.Count;

    public void Add(Voice voice) => _items[voice.Id.Value] = voice;

    public Task AddAsync(Voice voice, CancellationToken ct = default)
    {
        Add(voice);
        return Task.CompletedTask;
    }

    public Task<Voice?> GetByIdAsync(VoiceId id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id.Value));

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

internal sealed class MemoryPageCatalogReader(MemoryVoices voices, MemoryCounterLedger ledger) : IPageCatalogReader
{
    public async Task<PageCatalog> ReadAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        var loaded = await voices.ListByPageIdsAsync([pageId], cancellationToken);
        var rows = new CatalogVoice[loaded.Count];
        for (var i = 0; i < loaded.Count; i++)
        {
            var voice = loaded[i];
            rows[i] = new CatalogVoice(
                voice.Id.Value,
                voice.Title.Value,
                voice.DurationSeconds,
                voice.Status.ToString(),
                ledger.PlayCountOf(voice.Id.Value));
        }

        return new PageCatalog(ledger.SubscriberCountOf(pageId.Value), rows);
    }
}

public sealed class MemoryMappings : IWaveformMappingRepository
{
    private readonly Dictionary<Guid, WaveformMapping> _items = [];
    private long _nextSeedCode = 1;

    public long NextSeedCode() => _nextSeedCode++;

    public void Add(WaveformMapping mapping) => _items[mapping.Id.Value] = mapping;

    public Task AddAsync(WaveformMapping mapping, CancellationToken ct = default)
    {
        Add(mapping);
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

public sealed class MemoryCodes : IWaveformCodeAllocator
{
    private long _next = 1_000;

    public Task<WaveformCode> AllocateAsync(CancellationToken ct = default)
    {
        var code = WaveformCode.From(_next);
        _next++;
        return Task.FromResult(code);
    }
}

public sealed class MemoryUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Saves++;
        return Task.FromResult(1);
    }
}

public sealed class MemoryPackager : IAudioPackager
{
    public int Calls { get; private set; }

    public Task<AudioPackageResult> PackageAsync(
        string sourceStorageKey,
        double maxDurationSeconds,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<AudioPackageResult>(new AudioPackageResult.Failed("The API test host does not package audio."));
    }
}

public sealed class MemoryBlobs : IBlobStorage
{
    public List<string> Puts { get; } = [];

    public string Playlist { get; set; } = "#EXTM3U\n#EXTINF:1.0,\nseg000.aac\n";

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PutAsync(BlobUpload upload, CancellationToken cancellationToken)
    {
        Puts.Add(upload.StorageKey);
        return Task.CompletedTask;
    }

    public Task<string> ReadTextAsync(string storageKey, int maxChars, CancellationToken cancellationToken) =>
        Task.FromResult(Playlist);
}
