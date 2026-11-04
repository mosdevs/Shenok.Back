using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Infrastructure.Configurations;

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id)
            .HasConversion(id => id.Value, value => OtpChallengeId.From(value));
        builder.Property(challenge => challenge.UserId)
            .HasConversion(id => id.Value, value => UserId.From(value));
        builder.Property(challenge => challenge.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.Destination).HasMaxLength(256).IsRequired();
        builder.Property(challenge => challenge.Purpose).HasConversion<string>().HasMaxLength(20);
        builder.Property(challenge => challenge.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(challenge => challenge.DomainEvents);
        builder.HasIndex(challenge => new { challenge.UserId, challenge.Purpose, challenge.Destination });
    }
}
