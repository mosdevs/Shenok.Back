using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Infrastructure.Persistence.Conversions;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Configurations;

public sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("pages", table =>
        {
            table.HasCheckConstraint(
                "ck_pages_status",
                "status IN ('Draft', 'Published', 'Archived')");
            table.HasCheckConstraint(
                "ck_pages_voice_ids_array",
                "jsonb_typeof(voice_ids) = 'array'");
        });

        builder.HasKey(page => page.Id);
        builder.Property(page => page.Id)
            .HasConversion(id => id.Value, value => PageId.From(value))
            .HasColumnName("id");
        builder.Property(page => page.OwnerId)
            .HasConversion(id => id.Value, value => OwnerId.From(value))
            .HasColumnName("owner_id")
            .IsRequired();
        builder.HasIndex(page => page.OwnerId).HasDatabaseName("ix_pages_owner_id");
        builder.Property(page => page.Title)
            .HasConversion(title => title.Value, value => PageTitle.Create(value))
            .HasMaxLength(PageTitle.MaxLength)
            .HasColumnName("title")
            .IsRequired();
        builder.Property(page => page.Description)
            .HasConversion(description => description.Value, value => PageDescription.Create(value))
            .HasMaxLength(PageDescription.MaxLength)
            .HasColumnName("description")
            .IsRequired();
        builder.Property(page => page.CoverImageUrl)
            .HasConversion(
                url => url == null ? null : url.Value,
                value => string.IsNullOrEmpty(value) ? null : CoverImageUrl.Create(value))
            .HasMaxLength(CoverImageUrl.MaxLength)
            .HasColumnName("cover_image_url");
        builder.Property(page => page.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status")
            .IsRequired();
        builder.Property(page => page.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(page => page.VoiceIds)
            .HasField("_voiceIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(VoiceIdListConversion.Converter, VoiceIdListConversion.Comparer)
            .HasColumnName("voice_ids")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Ignore(page => page.DomainEvents);
    }
}
