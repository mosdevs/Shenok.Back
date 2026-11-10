using Microsoft.Extensions.Logging.Abstractions;
using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Tests;

public class ApplyWaveformUseCaseTests
{
    [Fact]
    public async Task Register_on_an_empty_index_adds_one_active_row_one_inbox_message_and_saves_once()
    {
        var harness = new ApplyHarness();
        var request = RegisterRequest();

        await harness.Register.ExecuteAsync(request, CancellationToken.None);

        var row = harness.Store.Peek(request.WaveformCode);
        Assert.NotNull(row);
        Assert.True(row.IsActive);
        Assert.Equal(request.WaveformId, row.WaveformId.Value);
        Assert.Equal(WaveformTargetType.Page, row.TargetType);
        Assert.Equal(request.TargetId, row.TargetId);
        Assert.Equal(request.LayoutVersion, row.LayoutVersion);
        Assert.Equal(request.EventId, row.SourceEventId);
        Assert.Equal(request.OccurredOn, row.SourceOccurredOn);
        Assert.Equal(1, harness.Store.Index.Adds);
        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.UnitOfWork.Saves);
        var message = Assert.Single(harness.Store.Inbox.Messages);
        Assert.Equal(request.EventId, message.EventId);
        Assert.Equal(WaveformMessageType.WaveformRegistered, message.MessageType);
        Assert.Equal(DateTimeKind.Utc, message.ReceivedOn.Kind);
    }

    [Fact]
    public async Task A_second_delivery_of_the_same_event_does_not_add_or_save()
    {
        var harness = new ApplyHarness();
        var request = RegisterRequest();

        await harness.Register.ExecuteAsync(request, CancellationToken.None);
        await harness.Register.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(1, harness.Store.Index.Adds);
        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.Inbox.Count);
        Assert.Equal(1, harness.Store.UnitOfWork.Saves);
        Assert.Equal(1, harness.Store.UnitOfWork.Attempts);
    }

    [Fact]
    public async Task Revoke_after_register_hides_the_row_and_a_second_revoke_does_not_add_another()
    {
        var harness = new ApplyHarness();
        var registered = RegisterRequest(occurredOn: At(10, 0));
        var revoked = RevokeRequest(
            code: registered.WaveformCode,
            waveformId: registered.WaveformId,
            targetId: registered.TargetId,
            occurredOn: At(10, 30));

        await harness.Register.ExecuteAsync(registered, CancellationToken.None);
        await harness.Revoke.ExecuteAsync(revoked, CancellationToken.None);

        Assert.Null(await harness.Store.Index.GetActiveAsync(WaveformCode.From(registered.WaveformCode)));
        var row = harness.Store.Peek(registered.WaveformCode);
        Assert.NotNull(row);
        Assert.Equal(WaveformIndexStatus.Revoked, row.Status);
        Assert.Equal(registered.WaveformId, row.WaveformId.Value);
        Assert.Equal(WaveformTargetType.Page, row.TargetType);
        Assert.Equal(registered.TargetId, row.TargetId);
        Assert.Equal(registered.LayoutVersion, row.LayoutVersion);
        Assert.Equal(revoked.EventId, row.SourceEventId);
        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.Index.Adds);

        await harness.Revoke.ExecuteAsync(revoked, CancellationToken.None);

        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.Index.Adds);
        Assert.Equal(2, harness.Store.Inbox.Count);
        Assert.Equal(2, harness.Store.UnitOfWork.Saves);
    }

    [Fact]
    public async Task An_older_register_after_revoke_first_stays_revoked_and_still_consumes_the_event()
    {
        var harness = new ApplyHarness();
        var revoked = RevokeRequest(occurredOn: At(10, 30), layoutVersion: 1);
        var older = RegisterRequest(
            code: revoked.WaveformCode,
            occurredOn: At(10, 0),
            layoutVersion: 2);

        await harness.Revoke.ExecuteAsync(revoked, CancellationToken.None);
        await harness.Register.ExecuteAsync(older, CancellationToken.None);

        var row = harness.Store.Peek(revoked.WaveformCode);
        Assert.NotNull(row);
        Assert.Equal(WaveformIndexStatus.Revoked, row.Status);
        Assert.False(row.IsActive);
        Assert.Equal(revoked.WaveformId, row.WaveformId.Value);
        Assert.Equal(WaveformTargetType.Voice, row.TargetType);
        Assert.Equal(revoked.TargetId, row.TargetId);
        Assert.Equal(revoked.EventId, row.SourceEventId);
        Assert.Equal(1, row.LayoutVersion);
        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.Index.Adds);
        Assert.Equal(2, harness.Store.Inbox.Count);
        Assert.Equal(2, harness.Store.UnitOfWork.Saves);
        Assert.Contains(harness.Store.Inbox.Messages, message => message.EventId == older.EventId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("page")]
    public async Task A_target_that_is_not_page_or_voice_is_rejected_before_any_write(string targetType)
    {
        var harness = new ApplyHarness();

        var registered = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Register.ExecuteAsync(RegisterRequest() with { TargetType = targetType }, CancellationToken.None));
        var revoked = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.Revoke.ExecuteAsync(RevokeRequest() with { TargetType = targetType }, CancellationToken.None));

        Assert.Equal("targetType", registered.ParamName);
        Assert.Equal("targetType", revoked.ParamName);
        Assert.Equal(0, harness.Store.Index.Adds);
        Assert.Equal(0, harness.Store.Inbox.Adds);
        Assert.Equal(0, harness.Store.UnitOfWork.Attempts);
        Assert.Equal(0, harness.Store.Index.Count);
        Assert.Equal(0, harness.Store.Inbox.Count);
    }

    [Fact]
    public async Task Register_normalizes_local_and_unspecified_timestamps_to_utc()
    {
        var harness = new ApplyHarness();
        var unspecified = new DateTime(2026, 11, 9, 10, 15, 30, DateTimeKind.Unspecified);
        var local = new DateTime(2026, 11, 9, 13, 45, 0, DateTimeKind.Local);

        await harness.Register.ExecuteAsync(RegisterRequest(code: 42, occurredOn: unspecified), CancellationToken.None);
        await harness.Register.ExecuteAsync(RegisterRequest(code: 43, occurredOn: local), CancellationToken.None);

        var stamped = harness.Store.Peek(42);
        var converted = harness.Store.Peek(43);
        Assert.NotNull(stamped);
        Assert.NotNull(converted);
        Assert.Equal(DateTimeKind.Utc, stamped.SourceOccurredOn.Kind);
        Assert.Equal(unspecified.Ticks, stamped.SourceOccurredOn.Ticks);
        Assert.Equal(DateTimeKind.Utc, converted.SourceOccurredOn.Kind);
        Assert.Equal(local.ToUniversalTime(), converted.SourceOccurredOn);
    }

    [Fact]
    public async Task Inbox_already_processed_from_save_completes_the_apply()
    {
        var harness = new ApplyHarness();
        harness.Store.UnitOfWork.Failure = new InboxAlreadyProcessedException();

        var exception = await Record.ExceptionAsync(() =>
            harness.Register.ExecuteAsync(RegisterRequest(), CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(0, harness.Store.UnitOfWork.Saves);
        Assert.Equal(0, harness.Store.Index.Count);
        Assert.Equal(0, harness.Store.Inbox.Count);
    }

    [Fact]
    public async Task Index_concurrency_is_retried_and_can_commit_on_a_later_attempt()
    {
        var harness = new ApplyHarness();
        harness.Store.UnitOfWork.RemainingConcurrencyFailures = 2;

        await harness.Register.ExecuteAsync(RegisterRequest(), CancellationToken.None);

        Assert.Equal(3, harness.Store.UnitOfWork.Attempts);
        Assert.Equal(1, harness.Store.UnitOfWork.Saves);
        Assert.Equal(3, harness.Store.Index.Gets);
        Assert.Equal(3, harness.Store.Index.Adds);
        Assert.Equal(1, harness.Store.Index.Count);
        Assert.Equal(1, harness.Store.Inbox.Count);
        var row = harness.Store.Peek(42);
        Assert.NotNull(row);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task The_third_index_concurrency_failure_propagates()
    {
        var harness = new ApplyHarness();
        harness.Store.UnitOfWork.RemainingConcurrencyFailures = 3;

        await Assert.ThrowsAsync<IndexConcurrencyException>(() =>
            harness.Register.ExecuteAsync(RegisterRequest(), CancellationToken.None));

        Assert.Equal(3, harness.Store.UnitOfWork.Attempts);
        Assert.Equal(3, harness.Store.Index.Gets);
        Assert.Equal(0, harness.Store.UnitOfWork.Saves);
        Assert.Equal(0, harness.Store.Index.Count);
        Assert.Equal(0, harness.Store.Inbox.Count);
    }

    private static ApplyWaveformRegisteredRequest RegisterRequest(
        long code = 42,
        DateTime? occurredOn = null,
        Guid? waveformId = null,
        Guid? targetId = null,
        int layoutVersion = 1)
        => new(
            Guid.NewGuid(),
            occurredOn ?? At(10, 15),
            waveformId ?? Guid.NewGuid(),
            code,
            layoutVersion,
            nameof(WaveformTargetType.Page),
            targetId ?? Guid.NewGuid());

    private static ApplyWaveformRevokedRequest RevokeRequest(
        long code = 42,
        DateTime? occurredOn = null,
        Guid? waveformId = null,
        Guid? targetId = null,
        int layoutVersion = 1)
        => new(
            Guid.NewGuid(),
            occurredOn ?? At(10, 15),
            waveformId ?? Guid.NewGuid(),
            code,
            layoutVersion,
            nameof(WaveformTargetType.Voice),
            targetId ?? Guid.NewGuid());

    private static DateTime At(int hour, int minute)
        => new(2026, 11, 9, hour, minute, 0, DateTimeKind.Utc);

    private sealed class ApplyHarness
    {
        public ApplyHarness()
        {
            Store = new RecognitionFakes();
            Register = new ApplyWaveformRegisteredUseCase(
                Store.Index,
                Store.Inbox,
                Store.UnitOfWork,
                NullLogger<ApplyWaveformRegisteredUseCase>.Instance);
            Revoke = new ApplyWaveformRevokedUseCase(
                Store.Index,
                Store.Inbox,
                Store.UnitOfWork,
                NullLogger<ApplyWaveformRevokedUseCase>.Instance);
        }

        public RecognitionFakes Store { get; }

        public ApplyWaveformRegisteredUseCase Register { get; }

        public ApplyWaveformRevokedUseCase Revoke { get; }
    }
}
