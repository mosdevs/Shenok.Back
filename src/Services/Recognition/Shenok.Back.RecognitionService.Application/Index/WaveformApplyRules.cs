using System.Linq.Expressions;
using FluentValidation;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Application.Index;

/// <summary>
/// The checks both bus validators apply. A new rule lives here, so register and revoke cannot drift.
/// Target names are the same check the apply path uses before it builds a <see cref="WaveformFact"/>.
/// </summary>
internal static class WaveformApplyRules
{
    public static void Add(AbstractValidator<ApplyWaveformRegisteredRequest> validator)
        => AddCore(
            validator,
            request => request.EventId,
            request => request.WaveformId,
            request => request.TargetId,
            request => request.WaveformCode,
            request => request.LayoutVersion,
            request => request.TargetType,
            request => request.OccurredOn);

    public static void Add(AbstractValidator<ApplyWaveformRevokedRequest> validator)
        => AddCore(
            validator,
            request => request.EventId,
            request => request.WaveformId,
            request => request.TargetId,
            request => request.WaveformCode,
            request => request.LayoutVersion,
            request => request.TargetType,
            request => request.OccurredOn);

    /// <summary>
    /// Accepts <c>Page</c> and <c>Voice</c> only. Numeric strings such as <c>0</c> are rejected.
    /// <c>Enum.TryParse</c> would map those onto the enum.
    /// </summary>
    public static WaveformTargetType ParseTarget(string targetType)
    {
        if (TryParseTarget(targetType, out var parsed))
            return parsed;

        throw new ArgumentException("Target type must be Page or Voice.", nameof(targetType));
    }

    public static bool IsPageOrVoice(string? targetType)
        => TryParseTarget(targetType, out _);

    private static bool TryParseTarget(string? targetType, out WaveformTargetType parsed)
    {
        switch (targetType)
        {
            case nameof(WaveformTargetType.Page):
                parsed = WaveformTargetType.Page;
                return true;
            case nameof(WaveformTargetType.Voice):
                parsed = WaveformTargetType.Voice;
                return true;
            default:
                parsed = default;
                return false;
        }
    }

    private static void AddCore<TRequest>(
        AbstractValidator<TRequest> validator,
        Expression<Func<TRequest, Guid>> eventId,
        Expression<Func<TRequest, Guid>> waveformId,
        Expression<Func<TRequest, Guid>> targetId,
        Expression<Func<TRequest, long>> waveformCode,
        Expression<Func<TRequest, int>> layoutVersion,
        Expression<Func<TRequest, string>> targetType,
        Expression<Func<TRequest, DateTime>> occurredOn)
    {
        validator.RuleFor(eventId).NotEmpty();
        validator.RuleFor(waveformId).NotEmpty();
        validator.RuleFor(targetId).NotEmpty();
        validator.RuleFor(waveformCode).InclusiveBetween(1L, WaveformCode.MaxValue);
        validator.RuleFor(layoutVersion).GreaterThanOrEqualTo(LayoutVersion.Version1);
        validator.RuleFor(targetType)
            .Must(static type => IsPageOrVoice(type))
            .WithMessage("Target type must be Page or Voice.");
        validator.RuleFor(occurredOn).Must(static value => value != default);
    }
}
