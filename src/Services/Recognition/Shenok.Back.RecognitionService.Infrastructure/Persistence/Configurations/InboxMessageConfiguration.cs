using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public const string TableName = "inbox_messages";
    public const string PrimaryKeyName = "pk_inbox_messages";
    public const string TypeCheckName = "ck_inbox_messages_type";

    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable(TableName, table =>
            table.HasCheckConstraint(
                TypeCheckName,
                $"message_type IN ('{nameof(WaveformMessageType.WaveformRegistered)}', '{nameof(WaveformMessageType.WaveformRevoked)}')"));

        builder.HasKey(message => message.EventId).HasName(PrimaryKeyName);
        builder.Property(message => message.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();
        builder.Property(message => message.MessageType)
            .HasConversion<string>()
            .HasMaxLength(64)
            .HasColumnName("message_type")
            .IsRequired();
        builder.Property(message => message.ReceivedOn)
            .HasColumnName("received_on")
            .IsRequired();
    }
}
