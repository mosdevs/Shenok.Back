using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class WaveformMappingConfiguration : IEntityTypeConfiguration<WaveformMapping>
{
    public const string CodeIndexName = "ix_waveform_mappings_waveform_code";

    public void Configure(EntityTypeBuilder<WaveformMapping> builder)
    {
        builder.ToTable("waveform_mappings", table =>
        {
            table.HasCheckConstraint(
                "ck_waveform_mappings_code_range",
                $"waveform_code > 0 AND waveform_code <= {WaveformCode.MaxValue}");
            table.HasCheckConstraint(
                "ck_waveform_mappings_target_type",
                "target_type IN ('Page', 'Voice')");
            table.HasCheckConstraint(
                "ck_waveform_mappings_status",
                "status IN ('Active', 'Revoked')");
            table.HasCheckConstraint(
                "ck_waveform_mappings_layout_version",
                $"layout_version = {LayoutVersion.Version1}");
        });

        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id)
            .HasConversion(id => id.Value, value => WaveformId.From(value))
            .HasColumnName("id");
        builder.Property(mapping => mapping.WaveformCode)
            .HasConversion(code => code.Value, value => WaveformCode.From(value))
            .HasColumnName("waveform_code")
            .IsRequired();
        builder.HasIndex(mapping => mapping.WaveformCode)
            .IsUnique()
            .HasDatabaseName(CodeIndexName);
        builder.Property(mapping => mapping.TargetType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("target_type")
            .IsRequired();
        builder.Property(mapping => mapping.TargetId).HasColumnName("target_id").IsRequired();
        builder.HasIndex(mapping => new { mapping.TargetType, mapping.TargetId })
            .HasDatabaseName("ix_waveform_mappings_target");
        builder.Property(mapping => mapping.LayoutVersion)
            .HasConversion(version => version.Value, value => LayoutVersion.From(value))
            .HasColumnName("layout_version")
            .IsRequired();
        builder.Property(mapping => mapping.RegisteredAt)
            .HasColumnName("registered_at")
            .IsRequired();
        builder.Property(mapping => mapping.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status")
            .IsRequired();
        builder.Ignore(mapping => mapping.IsActive);
        builder.Ignore(mapping => mapping.DomainEvents);
    }
}
