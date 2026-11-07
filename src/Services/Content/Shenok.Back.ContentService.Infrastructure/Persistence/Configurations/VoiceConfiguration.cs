using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class VoiceConfiguration : IEntityTypeConfiguration<Voice>
{
    public void Configure(EntityTypeBuilder<Voice> builder)
    {
        var maxDuration = AudioLimits.MaxDurationSeconds.ToString(CultureInfo.InvariantCulture);
        builder.ToTable("voices", table =>
        {
            table.HasCheckConstraint(
                "ck_voices_size_bytes",
                $"size_bytes > 0 AND size_bytes <= {AudioAsset.MaxSourceSizeBytes}");
            table.HasCheckConstraint(
                "ck_voices_packaging",
                $"""
                (
                    packaging_status = 'Pending'
                    AND duration_seconds IS NULL
                    AND playlist_storage_key IS NULL
                    AND segment_prefix IS NULL
                )
                OR
                (
                    packaging_status = 'Failed'
                    AND duration_seconds IS NULL
                    AND playlist_storage_key IS NULL
                    AND segment_prefix IS NULL
                )
                OR
                (
                    packaging_status = 'Ready'
                    AND duration_seconds > 0
                    AND duration_seconds <= {maxDuration}
                    AND playlist_storage_key IS NOT NULL
                    AND segment_prefix IS NOT NULL
                )
                """);
        });

        builder.HasKey(voice => voice.Id);
        builder.Property(voice => voice.Id)
            .HasConversion(id => id.Value, value => VoiceId.From(value))
            .HasColumnName("id");
        builder.Property(voice => voice.PageId)
            .HasConversion(id => id.Value, value => PageId.From(value))
            .HasColumnName("page_id")
            .IsRequired();
        builder.HasIndex(voice => voice.PageId).HasDatabaseName("ix_voices_page_id");
        builder.Property(voice => voice.Title)
            .HasConversion(title => title.Value, value => VoiceTitle.Create(value))
            .HasMaxLength(VoiceTitle.MaxLength)
            .HasColumnName("title")
            .IsRequired();
        builder.Property(voice => voice.DurationSeconds).HasColumnName("duration_seconds");
        builder.Ignore(voice => voice.Status);
        builder.Ignore(voice => voice.DomainEvents);

        builder.OwnsOne(voice => voice.Audio, audio =>
        {
            audio.Property(asset => asset.SourceStorageKey)
                .HasColumnName("source_storage_key")
                .HasMaxLength(AudioAsset.MaxKeyLength)
                .IsRequired();
            audio.Property(asset => asset.SourceContentType)
                .HasColumnName("source_content_type")
                .HasMaxLength(AudioAsset.MaxContentTypeLength)
                .IsRequired();
            audio.Property(asset => asset.SizeBytes).HasColumnName("size_bytes").IsRequired();
            audio.Property(asset => asset.PlaylistStorageKey)
                .HasColumnName("playlist_storage_key")
                .HasMaxLength(AudioAsset.MaxKeyLength);
            audio.Property(asset => asset.SegmentPrefix)
                .HasColumnName("segment_prefix")
                .HasMaxLength(AudioAsset.MaxKeyLength);
            audio.Property(asset => asset.PackagingStatus)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnName("packaging_status")
                .IsRequired();
            audio.HasIndex(asset => asset.SourceStorageKey)
                .IsUnique()
                .HasDatabaseName("ix_voices_source_storage_key");
            audio.HasIndex(asset => asset.PlaylistStorageKey)
                .IsUnique()
                .HasDatabaseName("ix_voices_playlist_storage_key");
            audio.HasIndex(asset => asset.SegmentPrefix)
                .IsUnique()
                .HasDatabaseName("ix_voices_segment_prefix");
            audio.HasIndex(asset => asset.PackagingStatus).HasDatabaseName("ix_voices_packaging_status");
        });
        builder.Navigation(voice => voice.Audio).IsRequired();
    }
}
