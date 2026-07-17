using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the canonical Transcript table.
/// </summary>
public sealed class TranscriptConfiguration : IEntityTypeConfiguration<Transcript>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Transcript> builder)
    {
        builder.ToTable("transcripts");
        builder.HasKey(x => x.TranscriptId);
        CanonicalFieldMapping.MapCanonicalColumns(builder);
    }
}
