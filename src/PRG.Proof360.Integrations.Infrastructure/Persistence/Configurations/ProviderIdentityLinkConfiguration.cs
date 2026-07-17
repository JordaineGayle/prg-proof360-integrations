using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for provider identity links.
/// Unique constraints are the correctness mechanism under concurrent duplicate delivery.
/// </summary>
public sealed class ProviderIdentityLinkConfiguration : IEntityTypeConfiguration<ProviderIdentityLink>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProviderIdentityLink> builder)
    {
        builder.ToTable("provider_identity_links");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProviderName).IsRequired();
        builder.Property(x => x.ProviderInstanceId).IsRequired();
        builder.Property(x => x.ExternalEntityType).IsRequired();
        builder.Property(x => x.ExternalId).IsRequired();
        builder.Property(x => x.CanonicalEntityType).IsRequired();

        // Database uniqueness beats check-then-insert races.
        builder.HasIndex(x => new { x.ProviderInstanceId, x.ExternalEntityType, x.ExternalId })
            .IsUnique()
            .HasDatabaseName("ux_identity_external");

        builder.HasIndex(x => new { x.ProviderInstanceId, x.CanonicalEntityType, x.CanonicalId })
            .IsUnique()
            .HasDatabaseName("ux_identity_canonical");

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasDefaultValue(0u);
    }
}
