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

        var revoked = registered.ApplyRevoked(FactFor(registered, occurredOn: Later, eventId: revokeEventId));

        Assert.Equal(IndexApplyResult.Applied, revoked);
        Assert.Equal(WaveformIndexStatus.Revoked, registered.Status);
        Assert.False(registered.IsActive);
        Assert.Equal(Later, registered.SourceOccurredOn);
        Assert.Equal(revokeEventId, registered.SourceEventId);

        var repeat = registered.ApplyRevoked(Fact(
            code: registered.Code,
            waveformId: WaveformId.New(),
            targetType: WaveformTargetType.Voice,
            occurredOn: Later,
            eventId: revokeEventId));

        Assert.Equal(IndexApplyResult.Unchanged, repeat);
        Assert.Equal(WaveformIndexStatus.Revoked, registered.Status);
        Assert.Equal(revokeEventId, registered.SourceEventId);
        Assert.Equal(LayoutVersion.Version1, registered.LayoutVersion);
    }

    [Fact]
    public void Revoke_first_then_an_older_register_is_stale_and_stays_revoked()
    {
        var revoked = RevokedEntry(SameInstant);
        var originalEventId = revoked.SourceEventId;
        var originalWaveformId = revoked.WaveformId;

        var result = revoked.ApplyRegistered(Fact(
            code: revoked.Code,
            waveformId: WaveformId.New(),
            targetType: WaveformTargetType.Voice,
            layoutVersion: 2,
            occurredOn: Earlier));

        Assert.Equal(IndexApplyResult.IgnoredStale, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.False(revoked.IsActive);
        Assert.Equal(originalEventId, revoked.SourceEventId);
        Assert.Equal(originalWaveformId, revoked.WaveformId);
        Assert.Equal(SameInstant, revoked.SourceOccurredOn);
        Assert.Equal(LayoutVersion.Version1, revoked.LayoutVersion);
    }

    [Fact]
    public void Revoke_first_then_a_newer_register_for_another_waveform_is_a_conflict_and_stays_revoked()
    {
        var revoked = RevokedEntry(SameInstant);
        var originalWaveformId = revoked.WaveformId;

        var result = revoked.ApplyRegistered(Fact(
            code: revoked.Code,
            waveformId: WaveformId.New(),
            targetType: revoked.TargetType,
            targetId: revoked.TargetId,
            layoutVersion: 2,
            occurredOn: Later));

        Assert.Equal(IndexApplyResult.IgnoredConflict, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.Equal(originalWaveformId, revoked.WaveformId);
        Assert.Equal(SameInstant, revoked.SourceOccurredOn);
        Assert.Equal(LayoutVersion.Version1, revoked.LayoutVersion);
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

        var result = entry.ApplyRegistered(Fact(
            code: code,
            waveformId: waveformId,
            targetType: WaveformTargetType.Voice,
            layoutVersion: 2,
            occurredOn: Later));

        Assert.Equal(IndexApplyResult.IgnoredConflict, result);
        Assert.Equal(code, entry.Code);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
        Assert.Equal(eventId, entry.SourceEventId);
        Assert.Equal(LayoutVersion.Version1, entry.LayoutVersion);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Newer_register_for_the_same_waveform_and_target_is_applied_and_moves_the_source_event()
    {
        var entry = Entry();
        var waveformId = entry.WaveformId;
        var nextEventId = Guid.NewGuid();

        var result = entry.ApplyRegistered(FactFor(
            entry,
            occurredOn: Later,
            eventId: nextEventId));

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(nextEventId, entry.SourceEventId);
        Assert.Equal(Later, entry.SourceOccurredOn);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
    }

    [Fact]
    public void Newer_register_for_the_same_identity_stores_the_incoming_layout()
    {
        var entry = Entry();
        var waveformId = entry.WaveformId;
        var targetId = entry.TargetId;
        var nextEventId = Guid.NewGuid();

        var result = entry.ApplyRegistered(FactFor(
            entry,
            layoutVersion: 2,
            occurredOn: Later,
            eventId: nextEventId));

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(2, entry.LayoutVersion);
        Assert.Equal(nextEventId, entry.SourceEventId);
        Assert.Equal(Later, entry.SourceOccurredOn);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(targetId, entry.TargetId);
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

        var result = entry.ApplyRevoked(Fact(
            code: entry.Code,
            waveformId: WaveformId.New(),
            targetType: WaveformTargetType.Voice,
            layoutVersion: 2,
            occurredOn: SameInstant,
            eventId: revokeEventId));

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(WaveformIndexStatus.Revoked, entry.Status);
        Assert.False(entry.IsActive);
        Assert.Equal(revokeEventId, entry.SourceEventId);
        Assert.Equal(SameInstant, entry.SourceOccurredOn);
        Assert.Equal(waveformId, entry.WaveformId);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(targetId, entry.TargetId);
        Assert.Equal(LayoutVersion.Version1, entry.LayoutVersion);
    }

    [Fact]
    public void Equal_timestamp_register_against_a_tombstone_is_a_conflict()
    {
        var revoked = RevokedEntry(SameInstant);
        var eventId = revoked.SourceEventId;

        var result = revoked.ApplyRegistered(FactFor(revoked));

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

        var result = revoked.ApplyRevoked(Fact(
            code: revoked.Code,
            waveformId: WaveformId.New(),
            targetType: WaveformTargetType.Voice,
            layoutVersion: 2,
            occurredOn: Later,
            eventId: nextEventId));

        Assert.Equal(IndexApplyResult.Applied, result);
        Assert.Equal(WaveformIndexStatus.Revoked, revoked.Status);
        Assert.Equal(nextEventId, revoked.SourceEventId);
        Assert.Equal(Later, revoked.SourceOccurredOn);
        Assert.Equal(waveformId, revoked.WaveformId);
        Assert.Equal(targetId, revoked.TargetId);
        Assert.Equal(LayoutVersion.Version1, revoked.LayoutVersion);
    }

    [Fact]
    public void Equal_timestamp_register_of_the_same_identity_does_not_move_the_source_event()
    {
        var entry = Entry(SameInstant);
        var eventId = entry.SourceEventId;

        var result = entry.ApplyRegistered(FactFor(entry, layoutVersion: 2));

        Assert.Equal(IndexApplyResult.Unchanged, result);
        Assert.Equal(eventId, entry.SourceEventId);
        Assert.Equal(WaveformIndexStatus.Active, entry.Status);
        Assert.Equal(LayoutVersion.Version1, entry.LayoutVersion);
    }

    [Fact]
    public void Newer_register_with_the_same_event_id_does_not_move_the_row()
    {
        var entry = Entry();
        var occurredOn = entry.SourceOccurredOn;
        var eventId = entry.SourceEventId;

        var result = entry.ApplyRegistered(FactFor(
            entry,
            layoutVersion: 2,
            occurredOn: Later,
            eventId: eventId));

        Assert.Equal(IndexApplyResult.Unchanged, result);
        Assert.Equal(occurredOn, entry.SourceOccurredOn);
        Assert.Equal(eventId, entry.SourceEventId);
        Assert.Equal(LayoutVersion.Version1, entry.LayoutVersion);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Older_revoke_with_the_same_event_id_is_stale()
    {
        var entry = Entry(SameInstant);

        var result = entry.ApplyRevoked(FactFor(entry, occurredOn: Earlier, eventId: entry.SourceEventId));

        Assert.Equal(IndexApplyResult.IgnoredStale, result);
        Assert.Equal(SameInstant, entry.SourceOccurredOn);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Factories_reject_an_empty_event_id_an_undefined_target_and_a_layout_before_version_1()
    {
        var code = WaveformCode.From(42);
        var waveformId = WaveformId.New();

        Assert.Throws<ArgumentException>(() => Fact(code: code, waveformId: waveformId, eventId: Guid.Empty));
        Assert.Throws<InvalidWaveformTargetException>(() =>
            Fact(code: code, waveformId: waveformId, targetType: (WaveformTargetType)99));
        Assert.Throws<InvalidWaveformTargetException>(() =>
            Fact(code: code, waveformId: waveformId, targetId: Guid.Empty));
        Assert.Throws<InvalidLayoutVersionException>(() =>
            Fact(code: code, waveformId: waveformId, layoutVersion: 0));
    }

    [Fact]
    public void Register_stores_a_layout_newer_than_version_1()
    {
        var entry = WaveformIndexEntry.Register(Fact(layoutVersion: 2));

        Assert.Equal(2, entry.LayoutVersion);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Apply_rejects_a_fact_for_a_different_code()
    {
        var entry = Entry();

        Assert.Throws<ArgumentException>(() => entry.ApplyRegistered(
            Fact(code: WaveformCode.From(99), waveformId: entry.WaveformId, occurredOn: Later)));
    }

    [Fact]
    public void Apply_rejects_a_timestamp_that_is_not_utc()
    {
        var entry = Entry();
        var local = DateTime.SpecifyKind(Later, DateTimeKind.Local);

        var ex = Assert.Throws<ArgumentException>(() => WaveformFact.Create(
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
        => WaveformIndexEntry.Register(Fact(occurredOn: occurredOn ?? SameInstant));

    private static WaveformIndexEntry RevokedEntry(DateTime occurredOn)
        => WaveformIndexEntry.RevokeFirst(Fact(occurredOn: occurredOn));

    private static WaveformFact FactFor(
        WaveformIndexEntry entry,
        WaveformTargetType? targetType = null,
        Guid? targetId = null,
        int? layoutVersion = null,
        DateTime? occurredOn = null,
        Guid? eventId = null)
        => Fact(
            code: entry.Code,
            waveformId: entry.WaveformId,
            targetType: targetType ?? entry.TargetType,
            targetId: targetId ?? entry.TargetId,
            layoutVersion: layoutVersion ?? entry.LayoutVersion,
            occurredOn: occurredOn ?? entry.SourceOccurredOn,
            eventId: eventId);

    private static WaveformFact Fact(
        WaveformCode? code = null,
        WaveformId? waveformId = null,
        WaveformTargetType targetType = WaveformTargetType.Page,
        Guid? targetId = null,
        int layoutVersion = LayoutVersion.Version1,
        DateTime? occurredOn = null,
        Guid? eventId = null)
        => WaveformFact.Create(
            code ?? WaveformCode.From(42),
            waveformId ?? WaveformId.New(),
            targetType,
            targetId ?? Guid.NewGuid(),
            layoutVersion,
            occurredOn ?? SameInstant,
            eventId ?? Guid.NewGuid());

    private static DateTime At(int hour, int minute)
        => new(2026, 11, 9, hour, minute, 30, DateTimeKind.Utc);
}
