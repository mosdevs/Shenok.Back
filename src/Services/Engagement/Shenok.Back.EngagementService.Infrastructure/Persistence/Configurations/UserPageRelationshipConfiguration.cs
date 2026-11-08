using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

public sealed class UserPageRelationshipConfiguration : IEntityTypeConfiguration<UserPageRelationship>
{
    public const string TableName = "user_page_relationships";
    public const string PrimaryKeyName = "pk_user_page_relationships";

    public void Configure(EntityTypeBuilder<UserPageRelationship> builder)
    {
        builder.ToTable(TableName);
        builder.Ignore(relationship => relationship.Id);
        builder.Ignore(relationship => relationship.DomainEvents);
        builder.HasKey(relationship => new { relationship.UserId, relationship.PageId })
            .HasName(PrimaryKeyName);

        builder.Property(relationship => relationship.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value))
            .HasColumnName("user_id")
            .IsRequired();
        builder.Property(relationship => relationship.PageId)
            .HasConversion(id => id.Value, value => PageId.From(value))
            .HasColumnName("page_id")
            .IsRequired();
        builder.Property(relationship => relationship.IsFollowing)
            .HasColumnName("is_following")
            .IsRequired();
        builder.Property(relationship => relationship.FollowedAt)
            .HasColumnName("followed_at");
        builder.Property(relationship => relationship.UnlockedAt)
            .HasColumnName("unlocked_at");

        builder.Property<uint>("xmin")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .HasColumnType("xid")
            .HasColumnName("xmin");
    }
}
