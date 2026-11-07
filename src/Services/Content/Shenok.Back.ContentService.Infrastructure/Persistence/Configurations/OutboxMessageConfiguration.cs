using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", table =>
            table.HasCheckConstraint(
                "ck_outbox_messages_type",
                $"message_type IN ('{OutboxMessage.RegisteredType}', '{OutboxMessage.RevokedType}')"));
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(message => message.Type)
            .HasMaxLength(64)
            .HasColumnName("message_type")
            .IsRequired();
        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(message => message.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();
        builder.Property(message => message.PublishedOn).HasColumnName("published_on");
        builder.HasIndex(message => message.PublishedOn)
            .HasFilter("published_on IS NULL")
            .HasDatabaseName("ix_outbox_messages_unpublished");
    }
}
