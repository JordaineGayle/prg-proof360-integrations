using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the canonical Vendor table.
/// </summary>
public sealed class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("vendors");
        builder.HasKey(x => x.VendorId);
        CanonicalFieldMapping.MapCanonicalColumns(builder);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.AiConfidence).HasPrecision(5, 4);
    }
}
