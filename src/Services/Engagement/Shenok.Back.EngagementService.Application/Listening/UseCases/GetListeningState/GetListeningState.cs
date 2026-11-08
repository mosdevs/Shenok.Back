using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;

public sealed record GetListeningStateRequest(Guid UserId, Guid PageId, IReadOnlyList<Guid> VoiceIds);

public sealed record ListeningVoiceState(Guid VoiceId, bool IsCompleted, int PositionSeconds);

public sealed record GetListeningStateResponse(
    bool IsFollowing,
    bool Unlocked,
    IReadOnlyList<ListeningVoiceState> Voices);

public sealed class GetListeningStateValidator : AbstractValidator<GetListeningStateRequest>
{
    public GetListeningStateValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.VoiceIds).NotNull();
        RuleForEach(x => x.VoiceIds).NotEmpty();
    }
}

/// <summary>
/// Returns follow and unlock for the page, plus one listening entry per requested voice.
/// A missing relationship is not followed and not unlocked. A missing voice is position 0 and not completed.
/// </summary>
public sealed class GetListeningStateUseCase(
    IUserPageRelationshipRepository relationships,
    IListeningRecordRepository listeningRecords) : IUseCase<GetListeningStateRequest, GetListeningStateResponse>
{
    public async Task<GetListeningStateResponse> ExecuteAsync(
        GetListeningStateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.VoiceIds);

        var userId = UserId.From(request.UserId);
        var pageId = PageId.From(request.PageId);
        var voiceIds = new VoiceId[request.VoiceIds.Count];
        for (var i = 0; i < request.VoiceIds.Count; i++)
            voiceIds[i] = VoiceId.From(request.VoiceIds[i]);

        var relationship = await relationships.GetAsync(userId, pageId, cancellationToken);
        var records = await listeningRecords.ListAsync(userId, voiceIds, cancellationToken);
        var stored = new Dictionary<Guid, ListeningRecord>(records.Count);
        foreach (var record in records)
            stored[record.VoiceId.Value] = record;

        var voices = new ListeningVoiceState[voiceIds.Length];
        for (var i = 0; i < voiceIds.Length; i++)
        {
            if (stored.TryGetValue(voiceIds[i].Value, out var record))
            {
                voices[i] = new ListeningVoiceState(voiceIds[i].Value, record.IsCompleted, record.Position.Value);
                continue;
            }

            voices[i] = new ListeningVoiceState(voiceIds[i].Value, IsCompleted: false, PositionSeconds: 0);
        }

        return new GetListeningStateResponse(
            relationship?.IsFollowing ?? false,
            relationship?.UnlockedAt is not null,
            voices);
    }
}
