using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for outbox messages.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName).IsRequired();
        builder.Property(x => x.ProviderInstanceId).IsRequired();
        builder.Property(x => x.OperationType).IsRequired();
        builder.Property(x => x.IdempotencyKey).IsRequired();
        builder.Property(x => x.CanonicalEntityType).IsRequired();
        builder.Property(x => x.CommandPayload).IsRequired();
        builder.Property(x => x.State).IsRequired();

        // Stable outbound idempotency keys cannot create duplicate provider writes.
        builder.HasIndex(x => new { x.ProviderInstanceId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_outbox_idempotency");

        builder.HasIndex(x => new { x.State, x.NextAttemptAt });

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasDefaultValue(0u);
    }
}
