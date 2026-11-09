using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Application.Tests;

public class ApplyWaveformValidatorTests
{
    [Fact]
    public void Both_validators_reject_poison_and_accept_a_legal_fact()
    {
        var registered = new ApplyWaveformRegisteredValidator();
        var revoked = new ApplyWaveformRevokedValidator();
        var legal = Legal();

        Assert.True(registered.Validate(legal).IsValid);
        Assert.True(revoked.Validate(LegalRevoke(legal)).IsValid);
        Assert.True(registered.Validate(legal with { TargetType = nameof(WaveformTargetType.Voice), LayoutVersion = 2, WaveformCode = WaveformCode.MaxValue }).IsValid);

        AssertInvalid(registered, revoked, legal with { EventId = Guid.Empty });
        AssertInvalid(registered, revoked, legal with { WaveformId = Guid.Empty });
        AssertInvalid(registered, revoked, legal with { TargetId = Guid.Empty });
        AssertInvalid(registered, revoked, legal with { WaveformCode = 0 });
        AssertInvalid(registered, revoked, legal with { WaveformCode = -1 });
        AssertInvalid(registered, revoked, legal with { WaveformCode = WaveformCode.MaxValue + 1 });
        AssertInvalid(registered, revoked, legal with { LayoutVersion = 0 });
        AssertInvalid(registered, revoked, legal with { TargetType = "page" });
        AssertInvalid(registered, revoked, legal with { TargetType = "image/jpeg; charset=binary" });
        AssertInvalid(registered, revoked, legal with { OccurredOn = default });
    }

    private static void AssertInvalid(
        ApplyWaveformRegisteredValidator registered,
        ApplyWaveformRevokedValidator revoked,
        ApplyWaveformRegisteredRequest request)
    {
        Assert.False(registered.Validate(request).IsValid);
        Assert.False(revoked.Validate(ToRevoke(request)).IsValid);
    }

    private static ApplyWaveformRegisteredRequest Legal()
        => new(
            Guid.NewGuid(),
            new DateTime(2026, 11, 9, 10, 15, 30, DateTimeKind.Utc),
            Guid.NewGuid(),
            42,
            LayoutVersion.Version1,
            nameof(WaveformTargetType.Page),
            Guid.NewGuid());

    private static ApplyWaveformRevokedRequest LegalRevoke(ApplyWaveformRegisteredRequest request)
        => ToRevoke(request);

    private static ApplyWaveformRevokedRequest ToRevoke(ApplyWaveformRegisteredRequest request)
        => new(
            request.EventId,
            request.OccurredOn,
            request.WaveformId,
            request.WaveformCode,
            request.LayoutVersion,
            request.TargetType,
            request.TargetId);
}
