using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the canonical Job table.
/// </summary>
public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(x => x.JobId);
        CanonicalFieldMapping.MapCanonicalColumns(builder);
        builder.Property(x => x.Source).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.AiConfidence).HasPrecision(5, 4);
        builder.HasIndex(x => x.AssignedVendorId);
        builder.HasIndex(x => x.Status);
    }
}
