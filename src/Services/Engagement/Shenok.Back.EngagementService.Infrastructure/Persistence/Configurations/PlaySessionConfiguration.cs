using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

public sealed class PlaySessionConfiguration : IEntityTypeConfiguration<PlaySession>
{
    public const string TableName = "play_sessions";
    public const string PrimaryKeyName = "pk_play_sessions";

    public void Configure(EntityTypeBuilder<PlaySession> builder)
    {
        builder.ToTable(TableName);
        builder.Ignore(session => session.DomainEvents);
        builder.HasKey(session => session.Id)
            .HasName(PrimaryKeyName);

        builder.Property(session => session.Id)
            .HasConversion(id => id.Value, value => PlaySessionId.From(value))
            .HasMaxLength(PlaySessionId.MaxLength)
            .HasColumnName("play_session_id")
            .IsRequired();
        builder.Property(session => session.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(session => session.VoiceId)
            .HasConversion(id => id.Value, value => VoiceId.From(value))
            .HasColumnName("voice_id")
            .IsRequired();
        builder.Property(session => session.Counted)
            .HasColumnName("counted")
            .IsRequired();

        builder.Property<uint>("xmin")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("xid")
            .HasColumnName("xmin");
    }
}
