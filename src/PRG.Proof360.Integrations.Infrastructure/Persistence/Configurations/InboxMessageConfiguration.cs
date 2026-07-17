using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for inbox messages.
/// </summary>
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName).IsRequired();
        builder.Property(x => x.ProviderInstanceId).IsRequired();
        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.EventType).IsRequired();
        builder.Property(x => x.PayloadEnvelope).IsRequired();
        builder.Property(x => x.PayloadHash).IsRequired();
        builder.Property(x => x.State).IsRequired();

        // Duplicate webhook/poll deliveries collapse here before canonical mutation.
        builder.HasIndex(x => new { x.ProviderInstanceId, x.EventId })
            .IsUnique()
            .HasDatabaseName("ux_inbox_event");

        builder.HasIndex(x => new { x.State, x.NextAttemptAt });

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasDefaultValue(0u);
    }
}
