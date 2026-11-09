using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Tests;

public class WaveformIndexEntryTests
{
    private static readonly DateTime Earlier = At(10, 0);
    private static readonly DateTime SameInstant = At(10, 15);
    private static readonly DateTime Later = At(10, 30);

    [Fact]
    public void Register_then_a_later_revoke_ends_revoked_and_a_repeat_of_that_event_is_unchanged()
    {
        var registered = Entry();
        var revokeEventId = Guid.NewGuid();

        var revoked = registered.ApplyRevoked(
            registered.Code,
            registered.WaveformId,
            registered.TargetType,
            registered.TargetId,
            registered.LayoutVersion,
            Later,
            revokeEventId);

        Assert.Equal(IndexApplyResult.Applied, revoked);
        Assert.Equal(WaveformIndexStatus.Revoked, registered.Status);
        Assert.False(registered.IsActive);
        Assert.Equal(Later, registered.SourceOccurredOn);
        Assert.Equal(revokeEventId, registered.SourceEventId);

        var repeat = registered.ApplyRevoked(
            registered.Code,
            WaveformId.New(),
            WaveformTargetType.Voice,
            Guid.NewGuid(),
            registered.LayoutVersion,
            Later,
            revokeEventId);

        Assert.Equal(IndexApplyResult.Unchanged, repeat);
        Assert.Equal(WaveformIndexStatus.Revoked, registered.Status);
        Assert.Equal(revokeEventId, registered.SourceEventId);
    }

    [Fact]
    public void Revoke_first_then_an_older_register_is_stale_and_stays_revoked()
    {
        var revoked = RevokedEntry(SameInstant);
        var originalEventId = revoked.SourceEventId;
        var originalWaveformId = revoked.WaveformId;

        var result = revoked.ApplyRegistered(
            revoked.Code,
            WaveformId.New(),
            WaveformTargetType.Voice,
            Guid.NewGuid(),
            LayoutVersion.Version1,
            Earlier,
            Guid.NewGuid());

        Assert.Equal(IndexApplyResult.IgnoredStale, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.False(revoked.IsActive);
        Assert.Equal(originalEventId, revoked.SourceEventId);
        Assert.Equal(originalWaveformId, revoked.WaveformId);
        Assert.Equal(SameInstant, revoked.SourceOccurredOn);
    }

    [Fact]
    public void Revoke_first_then_a_newer_register_for_another_waveform_is_a_conflict_and_stays_revoked()
    {
        var revoked = RevokedEntry(SameInstant);
        var originalWaveformId = revoked.WaveformId;

        var result = revoked.ApplyRegistered(
            revoked.Code,
            WaveformId.New(),
            revoked.TargetType,
            revoked.TargetId,
            LayoutVersion.Version1,
            Later,
            Guid.NewGuid());

        Assert.Equal(IndexApplyResult.IgnoredConflict, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.Equal(originalWaveformId, revoked.WaveformId);
        Assert.Equal(SameInstant, revoked.SourceOccurredOn);
    }

    [Fact]
    public void Newer_register_for_a_different_target_is_a_conflict_and_the_row_stays()
    {
        var entry = Entry();
        var code = entry.Code;
        var waveformId = entry.WaveformId;
        var targetType = entry.TargetType;
        var targetId = entry.TargetId;
        var eventId = entry.SourceEventId;

        var result = entry.ApplyRegistered(
            code,
            waveformId,
            WaveformTargetType.Voice,
            Guid.NewGuid(),
            entry.LayoutVersion,
            Later,
            Guid.NewGuid());

        Assert.Equal(IndexApplyResult.IgnoredConflict, result);
        Assert.Equal(code, entry.Code);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
        Assert.Equal(eventId, entry.SourceEventId);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Newer_register_for_the_same_waveform_and_target_is_applied_and_moves_the_source_event()
    {
        var entry = Entry();
        var waveformId = entry.WaveformId;
        var nextEventId = Guid.NewGuid();

        var result = entry.ApplyRegistered(
            entry.Code,
            waveformId,
            entry.TargetType,
            entry.TargetId,
            entry.LayoutVersion,
            Later,
            nextEventId);

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(nextEventId, entry.SourceEventId);
        Assert.Equal(Later, entry.SourceOccurredOn);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
    }

    [Fact]
    public void Equal_timestamp_revoke_with_a_different_event_wins_and_keeps_the_stored_target()
    {
        var entry = Entry(SameInstant);
        var waveformId = entry.WaveformId;
        var targetType = entry.TargetType;
        var targetId = entry.TargetId;
        var revokeEventId = Guid.NewGuid();

        var result = entry.ApplyRevoked(
            entry.Code,
            WaveformId.New(),
            WaveformTargetType.Voice,
            Guid.NewGuid(),
            entry.LayoutVersion,
            SameInstant,
            revokeEventId);

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(WaveformIndexStatus.Revoked, entry.Status);
        Assert.False(entry.IsActive);
        Assert.Equal(revokeEventId, entry.SourceEventId);
        Assert.Equal(SameInstant, entry.SourceOccurredOn);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
    }

    [Fact]
    public void Equal_timestamp_register_against_a_tombstone_is_a_conflict()
    {
        var revoked = RevokedEntry(SameInstant);
        var eventId = revoked.SourceEventId;

        var result = revoked.ApplyRegistered(
            revoked.Code,
            revoked.WaveformId,
            revoked.TargetType,
            revoked.TargetId,
            revoked.LayoutVersion,
            SameInstant,
            Guid.NewGuid());

        Assert.Equal(IndexApplyResult.IgnoredConflict, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.Equal(eventId, revoked.SourceEventId);
    }

    [Fact]
    public void Newer_revoke_of_an_already_revoked_row_moves_the_source_fact_and_keeps_the_target()
    {
        var revoked = RevokedEntry(SameInstant);
        var waveformId = revoked.WaveformId;
        var targetId = revoked.TargetId;
        var nextEventId = Guid.NewGuid();

        var result = revoked.ApplyRevoked(
            revoked.Code,
            WaveformId.New(),
            WaveformTargetType.Voice,
            Guid.NewGuid(),
            revoked.LayoutVersion,
            Later,
            nextEventId);

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.Equal(nextEventId, revoked.SourceEventId);
        Assert.Equal(Later, revoked.SourceOccurredOn);
        Assert.Equal(waveformId, revoked.WaveformId);
        Assert.Equal(targetId, revoked.TargetId);
    }

    [Fact]
    public void Equal_timestamp_register_of_the_same_identity_does_not_move_the_source_event()
    {
        var entry = Entry(SameInstant);
        var eventId = entry.SourceEventId;

        var result = entry.ApplyRegistered(
            entry.Code,
            entry.WaveformId,
            entry.TargetType,
            entry.TargetId,
            entry.LayoutVersion,
            SameInstant,
            Guid.NewGuid());

        Assert.Equal(IndexApplyResult.Unchanged, result);
        Assert.Equal(eventId, entry.SourceEventId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
    }

    [Fact]
    public void Older_revoke_with_the_same_event_id_is_stale()
    {
        var entry = Entry(SameInstant);

        var result = entry.ApplyRevoked(
            entry.Code,
            entry.WaveformId,
            entry.TargetType,
            entry.TargetId,
            entry.LayoutVersion,
            Earlier,
            entry.SourceEventId);

        Assert.Equal(IndexApplyResult.IgnoredStale, result);
        Assert.Equal(SameInstant, entry.SourceOccurredOn);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Factories_reject_an_empty_event_id_an_undefined_target_and_a_layout_before_version_1()
    {
        var code = WaveformCode.From(42);
        var waveformId = WaveformId.New();
        var targetId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Register(code, waveformId, eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Revoke(code, waveformId, eventId: Guid.Empty));
        Assert.Throws<InvalidWaveformTargetException>(() =>
            Register(code, waveformId, targetType: (WaveformTargetType)99));
        Assert.Throws<InvalidWaveformTargetException>(() =>
            Register(code, waveformId, targetId: Guid.Empty));
        Assert.Throws<InvalidLayoutVersionException>(() =>
            Register(code, waveformId, layoutVersion: 0));
    }

    [Fact]
    public void Register_stores_a_layout_newer_than_version_1()
    {
        var entry = Register(WaveformCode.From(42), WaveformId.New(), layoutVersion: 2);

        Assert.Equal(2, entry.LayoutVersion);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Apply_rejects_a_fact_for_a_different_code()
    {
        var entry = Entry();

        Assert.Throws<ArgumentException>(() => entry.ApplyRegistered(
            WaveformCode.From(99),
            entry.WaveformId,
            entry.TargetType,
            entry.TargetId,
            entry.LayoutVersion,
            Later,
            Guid.NewGuid()));
    }

    [Fact]
    public void Apply_rejects_a_timestamp_that_is_not_utc()
    {
        var entry = Entry();
        var local = DateTime.SpecifyKind(Later, DateTimeKind.Local);

        var ex = Assert.Throws<ArgumentException>(() => entry.ApplyRevoked(
            entry.Code,
            entry.WaveformId,
            entry.TargetType,
            entry.TargetId,
            entry.LayoutVersion,
            local,
            Guid.NewGuid()));

        Assert.Equal("occurredOn", ex.ParamName);
        Assert.True(entry.IsActive);
    }

    private static WaveformIndexEntry Entry(DateTime? occurredOn = null)
        => Register(WaveformCode.From(42), WaveformId.New(), occurredOn: occurredOn ?? SameInstant);

    private static WaveformIndexEntry RevokedEntry(DateTime occurredOn)
        => Revoke(WaveformCode.From(42), WaveformId.New(), occurredOn: occurredOn);

    private static WaveformIndexEntry Register(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType = WaveformTargetType.Page,
        Guid? targetId = null,
        int layoutVersion = LayoutVersion.Version1,
        DateTime? occurredOn = null,
        Guid? eventId = null)
        => WaveformIndexEntry.Register(
            code,
            waveformId,
            targetType,
            targetId ?? Guid.NewGuid(),
            layoutVersion,
            occurredOn ?? SameInstant,
            eventId ?? Guid.NewGuid());

    private static WaveformIndexEntry Revoke(
        WaveformCode code,
        WaveformId waveformId,
        Guid? eventId = null,
        DateTime? occurredOn = null)
        => WaveformIndexEntry.RevokeFirst(
            code,
            waveformId,
            WaveformTargetType.Page,
            Guid.NewGuid(),
            LayoutVersion.Version1,
            occurredOn ?? SameInstant,
            eventId ?? Guid.NewGuid());

    private static DateTime At(int hour, int minute)
        => new(2026, 11, 9, hour, minute, 30, DateTimeKind.Utc);
}
