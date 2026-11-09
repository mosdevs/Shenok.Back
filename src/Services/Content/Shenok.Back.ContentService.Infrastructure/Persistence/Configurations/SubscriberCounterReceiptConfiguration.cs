using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class SubscriberCounterReceiptConfiguration : IEntityTypeConfiguration<SubscriberCounterReceipt>
{
    public const string TableName = "subscriber_counter_receipts";
    public const string DeltaCheckName = "ck_subscriber_counter_receipts_delta";
    public const int KeyMaxLength = 128;

    public void Configure(EntityTypeBuilder<SubscriberCounterReceipt> builder)
    {
        builder.ToTable(TableName, table =>
            table.HasCheckConstraint(DeltaCheckName, "delta IN (1, -1)"));
        builder.HasKey(receipt => receipt.IdempotencyKey);
        builder.Property(receipt => receipt.IdempotencyKey)
            .HasMaxLength(KeyMaxLength)
            .HasColumnName("idempotency_key")
            .IsRequired();
        builder.Property(receipt => receipt.PageId)
            .HasColumnName("page_id")
            .IsRequired();
        builder.Property(receipt => receipt.Delta)
            .HasColumnName("delta")
            .IsRequired();
        builder.Property(receipt => receipt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
