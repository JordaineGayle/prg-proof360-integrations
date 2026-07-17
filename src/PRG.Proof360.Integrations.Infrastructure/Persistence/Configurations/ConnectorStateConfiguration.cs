using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for connector health/checkpoint state.
/// </summary>
public sealed class ConnectorStateConfiguration : IEntityTypeConfiguration<ConnectorState>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConnectorState> builder)
    {
        builder.ToTable("connector_states");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderName).IsRequired();
        builder.Property(x => x.ProviderInstanceId).IsRequired();
        builder.Property(x => x.CircuitState).IsRequired();
        builder.HasIndex(x => new { x.ProviderName, x.ProviderInstanceId })
            .IsUnique()
            .HasDatabaseName("ux_connector_state_instance");
    }
}
