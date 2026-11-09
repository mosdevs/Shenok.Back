using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.EngagementService.Domain.Repositories;
using Shenok.Back.EngagementService.Infrastructure.Counters;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class CounterCommandTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Follow_and_play_store_one_command_and_a_failed_save_stores_none()
    {
        var userId = UserId.New();
        var pageId = PageId.New();
        var voiceId = VoiceId.New();
        var sessionId = PlaySessionId.From("play-" + Guid.NewGuid().ToString("N"));
        Guid followEventId;
        Guid playEventId;

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var relationship = UserPageRelationship.Start(userId, pageId);
            Assert.True(relationship.Follow());
            followEventId = Assert.Single(relationship.DomainEvents.OfType<PageFollowedDomainEvent>()).EventId;
            await relationships.AddAsync(relationship, CancellationToken.None);

            var session = PlaySession.Open(sessionId, userId, voiceId);
            Assert.True(session.AcceptFirstReport());
            playEventId = Assert.Single(session.DomainEvents.OfType<VoicePlayedDomainEvent>()).EventId;
            await sessions.AddAsync(session, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using (var duplicateScope = fixture.CreateScope())
        {
            var relationships = duplicateScope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = duplicateScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var duplicate = UserPageRelationship.Start(userId, pageId);
            Assert.True(duplicate.Follow());
            await relationships.AddAsync(duplicate, CancellationToken.None);
            await Assert.ThrowsAsync<EngagementInsertConflictException>(() => unitOfWork.SaveChangesAsync(CancellationToken.None));
        }

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
            var handler = new RecordingHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://content.test/") };
            var publisher = new ContentCounterPublisher(db, new SingleClientFactory(client), NullLogger<ContentCounterPublisher>.Instance);
            await publisher.PublishSubscriberAsync(followEventId, userId.Value, pageId.Value, 1, CancellationToken.None);
            await publisher.PublishSubscriberAsync(followEventId, userId.Value, pageId.Value, 1, CancellationToken.None);
            Assert.Single(handler.Calls);
            Assert.Contains($"/internal/pages/{pageId.Value:D}/subscribers", handler.Calls[0].Path, StringComparison.Ordinal);

            handler.Status = HttpStatusCode.ServiceUnavailable;
            var unavailable = await Assert.ThrowsAsync<UnavailableException>(() =>
                publisher.PublishPlayAsync(playEventId, sessionId.Value, voiceId.Value, CancellationToken.None));
            Assert.Equal("Content counters are unavailable.", unavailable.Message);
        }

        await using var read = fixture.CreateBareContext();
        var commands = await read.CounterCommands.AsNoTracking()
            .Where(command => command.Id == followEventId || command.Id == playEventId)
            .ToListAsync();
        Assert.Equal(2, commands.Count);

        var follow = Assert.Single(commands, command => command.Id == followEventId);
        Assert.Equal(CounterCommand.SubscriberKey(userId.Value, pageId.Value, followEventId), follow.IdempotencyKey);
        Assert.Equal(1, follow.Delta);
        Assert.NotNull(follow.PublishedOn);

        var play = Assert.Single(commands, command => command.Id == playEventId);
        Assert.Equal(sessionId.Value, play.IdempotencyKey);
        Assert.Null(play.PublishedOn);
        Assert.Equal(0, await read.CounterCommands.CountAsync(command => command.PageId == pageId.Value && command.Id != followEventId));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Path, string Body)> Calls { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.RequestUri?.PathAndQuery ?? "", body));
            return new HttpResponseMessage(Status);
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
