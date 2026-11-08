using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

public sealed class ListeningRecordConfiguration : IEntityTypeConfiguration<ListeningRecord>
{
    public const string TableName = "listening_records";
    public const string PrimaryKeyName = "pk_listening_records";
    public const string PositionCheckName = "ck_listening_records_position_non_negative";

    public void Configure(EntityTypeBuilder<ListeningRecord> builder)
    {
        // PlaybackPosition.From rejects a negative value. The check stops a direct insert from storing one.
        builder.ToTable(TableName, table =>
            table.HasCheckConstraint(PositionCheckName, "position >= 0"));
        builder.Ignore(record => record.Id);
        builder.Ignore(record => record.DomainEvents);
        builder.HasKey(record => new { record.UserId, record.VoiceId })
            .HasName(PrimaryKeyName);

        builder.Property(record => record.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(record => record.VoiceId)
            .HasConversion(id => id.Value, value => VoiceId.From(value))
            .HasColumnName("voice_id")
            .IsRequired();
        builder.Property(record => record.Position)
            .HasConversion(position => position.Value, value => PlaybackPosition.From(value))
            .HasColumnName("position")
            .IsRequired();
        builder.Property(record => record.IsCompleted)
            .HasColumnName("is_completed")
            .IsRequired();
        builder.Property(record => record.LastPlayedAt)
            .HasColumnName("last_played_at");

        builder.Property<uint>("xmin")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("xid")
            .HasColumnName("xmin");
    }
}
