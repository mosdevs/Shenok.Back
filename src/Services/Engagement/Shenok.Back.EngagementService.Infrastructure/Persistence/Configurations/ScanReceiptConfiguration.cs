using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

public sealed class ScanReceiptConfiguration : IEntityTypeConfiguration<ScanReceipt>
{
    public const string TableName = "scan_receipts";
    public const string PrimaryKeyName = "pk_scan_receipts";
    public const string ResumePositionCheckName = "ck_scan_receipts_resume_position_non_negative";

    public void Configure(EntityTypeBuilder<ScanReceipt> builder)
    {
        // The stored resume position is a PlaybackPosition. The check stops a direct insert of a negative second count.
        builder.ToTable(TableName, table =>
            table.HasCheckConstraint(ResumePositionCheckName, "resume_position >= 0"));
        builder.Ignore(receipt => receipt.DomainEvents);
        builder.HasKey(receipt => receipt.Id)
            .HasName(PrimaryKeyName);

        builder.Property(receipt => receipt.Id)
            .HasConversion(id => id.Value, value => ScanId.From(value))
            .HasMaxLength(ScanId.MaxLength)
            .HasColumnName("scan_id")
            .IsRequired();
        builder.Property(receipt => receipt.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(receipt => receipt.WaveformId)
            .HasConversion(id => id.Value, value => WaveformId.From(value))
            .HasColumnName("waveform_id")
            .IsRequired();
        builder.Property(receipt => receipt.VoiceId)
            .HasConversion(id => id.Value, value => VoiceId.From(value))
            .HasColumnName("voice_id")
            .IsRequired();
        builder.Property(receipt => receipt.PageId)
            .HasConversion(id => id.Value, value => PageId.From(value))
            .HasColumnName("page_id")
            .IsRequired();
        builder.Property(receipt => receipt.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(receipt => receipt.Unlocked)
            .HasColumnName("unlocked")
            .IsRequired();
        builder.Property(receipt => receipt.IsFollowing)
            .HasColumnName("is_following")
            .IsRequired();
        builder.Property(receipt => receipt.PromptFollow)
            .HasColumnName("prompt_follow")
            .IsRequired();
        builder.Property(receipt => receipt.ResumePosition)
            .HasConversion(position => position.Value, value => PlaybackPosition.From(value))
            .HasColumnName("resume_position")
            .IsRequired();
    }
}
