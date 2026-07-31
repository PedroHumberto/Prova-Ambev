using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", table =>
        {
            table.HasCheckConstraint("CK_OutboxMessages_Attempts", "\"Attempts\" >= 0");
            table.HasCheckConstraint(
                "CK_OutboxMessages_FinalState",
                "NOT (\"PublishedAt\" IS NOT NULL AND \"DeadLetteredAt\" IS NOT NULL)");
        });

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(message => message.Type).IsRequired().HasMaxLength(200);
        builder.Property(message => message.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(message => message.OccurredAt).HasColumnType("timestamp with time zone");
        builder.Property(message => message.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(message => message.NextAttemptAt).HasColumnType("timestamp with time zone");
        builder.Property(message => message.LockId).HasColumnType("uuid");
        builder.Property(message => message.LockedUntil).HasColumnType("timestamp with time zone");
        builder.Property(message => message.PublishedAt).HasColumnType("timestamp with time zone");
        builder.Property(message => message.DeadLetteredAt).HasColumnType("timestamp with time zone");
        builder.Property(message => message.LastError).HasMaxLength(2000);

        builder.HasIndex(message => new
            {
                message.NextAttemptAt,
                message.CreatedAt
            })
            .HasFilter("\"PublishedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}
