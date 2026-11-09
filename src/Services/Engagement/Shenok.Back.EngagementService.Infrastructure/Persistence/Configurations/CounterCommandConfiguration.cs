using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

public sealed class CounterCommandConfiguration : IEntityTypeConfiguration<CounterCommand>
{
    public const string TableName = "content_counter_commands";
    public const string KindCheckName = "ck_content_counter_commands_kind";
    public const string ShapeCheckName = "ck_content_counter_commands_shape";
    public const string IdempotencyIndexName = "ux_content_counter_commands_idempotency_key";
    public const string UnpublishedIndexName = "ix_content_counter_commands_unpublished";

    public void Configure(EntityTypeBuilder<CounterCommand> builder)
    {
        builder.ToTable(TableName, table =>
        {
            table.HasCheckConstraint(
                KindCheckName,
                "command_kind IN ('Subscriber', 'Play')");
            table.HasCheckConstraint(
                ShapeCheckName,
                """
                (command_kind = 'Subscriber' AND user_id IS NOT NULL AND page_id IS NOT NULL AND voice_id IS NULL AND play_session_id IS NULL AND delta IN (1, -1))
                OR (command_kind = 'Play' AND user_id IS NULL AND page_id IS NULL AND voice_id IS NOT NULL AND play_session_id IS NOT NULL AND delta = 1)
                """);
        });
        builder.HasKey(command => command.Id);
        builder.Property(command => command.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(command => command.Kind)
            .HasMaxLength(16)
            .HasColumnName("command_kind")
            .IsRequired();
        builder.Property(command => command.IdempotencyKey)
            .HasMaxLength(128)
            .HasColumnName("idempotency_key")
            .IsRequired();
        builder.HasIndex(command => command.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName(IdempotencyIndexName);
        builder.Property(command => command.UserId).HasColumnName("user_id");
        builder.Property(command => command.PageId).HasColumnName("page_id");
        builder.Property(command => command.VoiceId).HasColumnName("voice_id");
        builder.Property(command => command.PlaySessionId)
            .HasMaxLength(64)
            .HasColumnName("play_session_id");
        builder.Property(command => command.Delta)
            .HasColumnName("delta")
            .IsRequired();
        builder.Property(command => command.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();
        builder.Property(command => command.PublishedOn).HasColumnName("published_on");
        builder.HasIndex(command => new { command.OccurredOn, command.Id })
            .HasFilter("published_on IS NULL")
            .HasDatabaseName(UnpublishedIndexName);
    }
}
