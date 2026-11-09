using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Zero row inserted with the page. Phase 6 updates <see cref="PageCounter.SubscriberCount"/> with guarded SQL.
/// </summary>
public sealed class PageCounterConfiguration : IEntityTypeConfiguration<PageCounter>
{
    public void Configure(EntityTypeBuilder<PageCounter> builder)
    {
        builder.ToTable("page_counters", table =>
            table.HasCheckConstraint("ck_page_counters_subscriber_non_negative", "subscriber_count >= 0"));
        builder.HasKey(counter => counter.PageId);
        builder.Property(counter => counter.PageId)
            .HasConversion(id => id.Value, value => PageId.From(value))
            .HasColumnName("page_id");
        builder.Property(counter => counter.SubscriberCount)
            .HasColumnName("subscriber_count")
            .HasDefaultValue(0L)
            .ValueGeneratedNever()
            .IsRequired();
    }
}
