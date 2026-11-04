using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Infrastructure.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id)
            .HasConversion(id => id.Value, value => RefreshTokenId.From(value));
        builder.Property(token => token.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value));
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.Ignore(token => token.DomainEvents);
    }
}
