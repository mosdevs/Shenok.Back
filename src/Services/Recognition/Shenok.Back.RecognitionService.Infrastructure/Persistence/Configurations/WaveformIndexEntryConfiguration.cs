using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence.Configurations;

public sealed class WaveformIndexEntryConfiguration : IEntityTypeConfiguration<WaveformIndexEntry>
{
    public const string TableName = "waveform_index";
    public const string PrimaryKeyName = "pk_waveform_index";
    public const string StatusCheckName = "ck_waveform_index_status";
    public const string TargetCheckName = "ck_waveform_index_target";
    public const string CodeCheckName = "ck_waveform_index_code";

    public void Configure(EntityTypeBuilder<WaveformIndexEntry> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint(StatusCheckName, "status IN ('Active', 'Revoked')");
            table.HasCheckConstraint(TargetCheckName, "target_type IN ('Page', 'Voice')");
            table.HasCheckConstraint(
                CodeCheckName,
                $"waveform_code > 0 AND waveform_code <= {WaveformCode.MaxValue}");
        });

        builder.HasKey(entry => entry.Code).HasName(PrimaryKeyName);
        builder.Property(entry => entry.Code)
            .HasConversion(code => code.Value, value => WaveformCode.From(value))
            .HasColumnName("waveform_code")
            .ValueGeneratedNever();
        builder.Property(entry => entry.WaveformId)
            .HasConversion(id => id.Value, value => WaveformId.From(value))
            .HasColumnName("waveform_id")
            .IsRequired();
        builder.Property(entry => entry.TargetType)
            .HasConversion<string>()
            .HasColumnName("target_type")
            .IsRequired();
        builder.Property(entry => entry.TargetId)
            .HasColumnName("target_id")
            .IsRequired();
        builder.Property(entry => entry.LayoutVersion)
            .HasColumnName("layout_version")
            .IsRequired();
        builder.Property(entry => entry.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();
        builder.Property(entry => entry.SourceOccurredOn)
            .HasColumnName("source_occurred_on")
            .IsRequired();
        builder.Property(entry => entry.SourceEventId)
            .HasColumnName("source_event_id")
            .IsRequired();
        builder.Ignore(entry => entry.IsActive);

        builder.Property<uint>("xmin")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("xid")
            .HasColumnName("xmin");
    }
}
