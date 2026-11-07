using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Schema only. Phase 6 updates <see cref="VoiceCounter.PlayCount"/> with guarded SQL.
/// </summary>
public sealed class VoiceCounterConfiguration : IEntityTypeConfiguration<VoiceCounter>
{
    public void Configure(EntityTypeBuilder<VoiceCounter> builder)
    {
        builder.ToTable("voice_counters", table =>
            table.HasCheckConstraint("ck_voice_counters_play_count_non_negative", "play_count >= 0"));
        builder.HasKey(counter => counter.VoiceId);
        builder.Property(counter => counter.VoiceId)
            .HasConversion(id => id.Value, value => VoiceId.From(value))
            .HasColumnName("voice_id");
        builder.Property(counter => counter.PlayCount)
            .HasColumnName("play_count")
            .HasDefaultValue(0L)
            .ValueGeneratedNever()
            .IsRequired();
    }
}
