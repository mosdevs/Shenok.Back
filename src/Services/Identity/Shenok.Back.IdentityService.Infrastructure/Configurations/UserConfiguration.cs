using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Infrastructure.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Id)
                .HasConversion(
                    userId => userId.Value,
                    value => UserId.From(value));

            builder.OwnsOne(u => u.Email, email =>
            {
                email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(256)
                .IsRequired(true);
            });

            builder.OwnsOne(u => u.PasswordHash, passwordHash =>
            {
                passwordHash.Property(p => p.Value)
                .HasColumnName("PasswordHash")
                .IsRequired(true);
            });

            builder.OwnsOne(u => u.Profile, profile =>
            {
                profile.Property(p => p.DisplayName)
                .HasColumnType("DisplayName")
                .HasMaxLength(50)
                .IsRequired(true);

                profile.Property(p => p.Bio)
                .HasColumnName("Bio")
                .HasMaxLength(500);

                profile.Property(p => p.AvatarUrl)
                .HasColumnName("AvatarUrl");
            });

            builder.Property(u => u.UserRole)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired(true);

            builder.Property(u => u.VerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(u => u.CreatedAt)
                .IsRequired(true);

            builder.Ignore(u => u.DomainEvents);
        }
    }
}
