using Microsoft.Extensions.Options;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;
using Shenok.Back.EngagementService.Application.Follows.UseCases.UnfollowPage;
using Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;
using Shenok.Back.EngagementService.Application.Listening.UseCases.ReportProgress;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;
using Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;

namespace Shenok.Back.EngagementService.Application.Tests;

internal sealed class EngagementHarness
{
    private readonly FakeReceipts _receipts;
    private readonly FakeRelationships _relationships;
    private readonly FakeListening _listening;
    private readonly FakeSessions _sessions;
    private readonly FakeUnitOfWork _unitOfWork;

    public EngagementHarness()
    {
        Memory = new EngagementMemory();
        _receipts = new FakeReceipts(Memory);
        _relationships = new FakeRelationships(Memory);
        _listening = new FakeListening(Memory);
        _sessions = new FakeSessions(Memory);
        _unitOfWork = new FakeUnitOfWork(Memory);
        Counters = new FakeContentCounters();
    }

    public EngagementMemory Memory { get; }

    public FakeContentCounters Counters { get; }

    public FakeRelationships Relationships => _relationships;

    public FakeListening Listening => _listening;

    public Guid UserId { get; } = Guid.NewGuid();

    public Guid PageId { get; } = Guid.NewGuid();

    public Guid VoiceId { get; } = Guid.NewGuid();

    public Guid WaveformId { get; } = Guid.NewGuid();

    public RegisterScanUseCase RegisterScan(bool autoFollow = false) =>
        new(
            _receipts,
            _relationships,
            _listening,
            Counters,
            _unitOfWork,
            Options.Create(new EngagementOptions { AutoFollowOnScan = autoFollow }));

    public FollowPageUseCase Follow() => new(_relationships, Counters, _unitOfWork);

    public UnfollowPageUseCase Unfollow() => new(_relationships, Counters, _unitOfWork);

    public ReportProgressUseCase ReportProgress() => new(_listening, _sessions, Counters, _unitOfWork);

    public GetListeningStateUseCase ListeningState() => new(_relationships, _listening);

    public CanPlayUseCase CanPlay() => new(_relationships);

    public RegisterScanRequest Scan(string scanId = "scan-1") =>
        new(scanId, UserId, WaveformId, VoiceId, PageId);

    public FollowPageRequest FollowRequest() => new(UserId, PageId);

    public UnfollowPageRequest UnfollowRequest() => new(UserId, PageId);

    public ReportProgressRequest Progress(
        string sessionId,
        int position,
        bool completed = false,
        bool isSeek = false) =>
        new(UserId, VoiceId, sessionId, position, completed, isSeek);
}
