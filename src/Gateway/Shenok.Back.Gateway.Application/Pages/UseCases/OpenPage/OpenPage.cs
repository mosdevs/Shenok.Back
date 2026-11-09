using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Pages.UseCases.OpenPage;

public sealed record OpenPageRequest(Guid UserId, Guid PageId, bool IsAdmin);

public sealed record OpenPageDocument(Guid Id, string Title, string? CoverUrl, long SubscriberCount);

public sealed record OpenVoiceDocument(
    Guid Id,
    string Title,
    double? Duration,
    bool IsCompleted,
    int PositionSeconds);

public sealed record OpenPageResponse(
    OpenPageDocument Page,
    bool IsFollowing,
    IReadOnlyList<OpenVoiceDocument> Voices);

public sealed class OpenPageValidator : AbstractValidator<OpenPageRequest>
{
    public OpenPageValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
    }
}

/// <summary>
/// Flow E. Merges the Content page with this caller's listening state.
/// The voice list has no playback URLs. A caller who is not the owner or an admin must already have unlocked the page.
/// </summary>
public sealed class OpenPageUseCase(IContentGateway content, IEngagementGateway engagement)
    : IUseCase<OpenPageRequest, OpenPageResponse>
{
    public async Task<OpenPageResponse> ExecuteAsync(OpenPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await content.GetPageAsync(request.PageId, cancellationToken);
        var voiceIds = new Guid[page.Voices.Count];
        for (var i = 0; i < page.Voices.Count; i++)
            voiceIds[i] = page.Voices[i].VoiceId;

        var listening = await engagement.GetListeningStateAsync(page.PageId, voiceIds, cancellationToken);
        if (request.UserId != page.OwnerId && !request.IsAdmin && !listening.Unlocked)
            throw new ForbiddenException(GatewayLimits.LockedDetail);

        var recorded = listening.Voices ?? [];
        var states = new Dictionary<Guid, ListeningVoice>(recorded.Count);
        foreach (var voice in recorded)
            states[voice.VoiceId] = voice;

        var merged = new OpenVoiceDocument[page.Voices.Count];
        for (var i = 0; i < page.Voices.Count; i++)
        {
            var voice = page.Voices[i];
            states.TryGetValue(voice.VoiceId, out var state);
            merged[i] = new OpenVoiceDocument(
                voice.VoiceId,
                voice.Title,
                voice.DurationSeconds,
                state?.IsCompleted ?? false,
                state?.PositionSeconds ?? 0);
        }

        return new OpenPageResponse(
            new OpenPageDocument(page.PageId, page.Title, page.CoverImageUrl, page.SubscriberCount),
            listening.IsFollowing,
            merged);
    }
}
