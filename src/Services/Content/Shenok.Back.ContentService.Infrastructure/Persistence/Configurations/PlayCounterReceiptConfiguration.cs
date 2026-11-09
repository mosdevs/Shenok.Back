using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class PlayCounterReceiptConfiguration : IEntityTypeConfiguration<PlayCounterReceipt>
{
    public const string TableName = "play_counter_receipts";
    public const int KeyMaxLength = 64;

    public void Configure(EntityTypeBuilder<PlayCounterReceipt> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(receipt => receipt.IdempotencyKey);
        builder.Property(receipt => receipt.IdempotencyKey)
            .HasMaxLength(KeyMaxLength)
            .HasColumnName("idempotency_key")
            .IsRequired();
        builder.Property(receipt => receipt.VoiceId)
            .HasColumnName("voice_id")
            .IsRequired();
        builder.Property(receipt => receipt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
