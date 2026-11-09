using Microsoft.EntityFrameworkCore;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence.Repositories;

public sealed class InboxRepository(RecognitionDbContext db) : IInbox
{
    public Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default) =>
        db.InboxMessages.AsNoTracking().AnyAsync(message => message.EventId == eventId, ct);

    public Task AddAsync(Guid eventId, WaveformMessageType messageType, DateTime receivedOn, CancellationToken ct = default)
    {
        db.InboxMessages.Add(new InboxMessage(eventId, messageType, receivedOn));
        return Task.CompletedTask;
    }
}
