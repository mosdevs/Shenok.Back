using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Infrastructure.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasConversion(id => id.Value, value => UserId.From(value));

        builder.OwnsOne(user => user.Email, email =>
        {
            email.Property(value => value.Value).HasColumnName("email").HasMaxLength(256);
            email.HasIndex(value => value.Value).IsUnique();
        });
        builder.Navigation(user => user.Email).IsRequired(false);

        builder.OwnsOne(user => user.PhoneNumber, phone =>
        {
            phone.Property(value => value.Value).HasColumnName("phone_number").HasMaxLength(16);
            phone.HasIndex(value => value.Value).IsUnique();
        });
        builder.Navigation(user => user.PhoneNumber).IsRequired(false);

        builder.OwnsOne(user => user.PasswordHash, hash =>
        {
            hash.Property(value => value.Value).HasColumnName("password_hash").HasMaxLength(512);
        });
        builder.Navigation(user => user.PasswordHash).IsRequired(false);

        builder.OwnsOne(user => user.Profile, profile =>
        {
            profile.Property(value => value.Username).HasColumnName("username").HasMaxLength(30).IsRequired();
            profile.HasIndex(value => value.Username).IsUnique();
            profile.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(50).IsRequired();
            profile.Property(value => value.Bio).HasColumnName("bio").HasMaxLength(500);
            profile.Property(value => value.AvatarUrl).HasColumnName("avatar_url");
        });

        builder.Property<string>("RolesStorage").HasColumnName("roles").HasMaxLength(100).IsRequired();
        builder.Property(user => user.VerificationStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(user => user.CreatedAt).IsRequired();
        builder.Ignore(user => user.DomainEvents);
        builder.Ignore(user => user.Roles);
    }
}
