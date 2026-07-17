using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for append-only audit events.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).IsRequired();
        builder.Property(x => x.ProviderName).IsRequired();
        builder.Property(x => x.Operation).IsRequired();
        builder.Property(x => x.Result).IsRequired();
        builder.HasIndex(x => x.CorrelationId);
        builder.HasIndex(x => x.Timestamp);
    }
}
