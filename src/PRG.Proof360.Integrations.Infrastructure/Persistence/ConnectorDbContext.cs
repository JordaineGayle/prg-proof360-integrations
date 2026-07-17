using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for canonical Proof360 entities and connector infrastructure records.
/// </summary>
public sealed class ConnectorDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectorDbContext"/> class.
    /// </summary>
    public ConnectorDbContext(DbContextOptions<ConnectorDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the Vendors set.</summary>
    public DbSet<Vendor> Vendors => Set<Vendor>();

    /// <summary>Gets the Jobs set.</summary>
    public DbSet<Job> Jobs => Set<Job>();

    /// <summary>Gets the Transcripts set.</summary>
    public DbSet<Transcript> Transcripts => Set<Transcript>();

    /// <summary>Gets the provider identity links set.</summary>
    public DbSet<ProviderIdentityLink> ProviderIdentityLinks => Set<ProviderIdentityLink>();

    /// <summary>Gets the inbox messages set.</summary>
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    /// <summary>Gets the outbox messages set.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Gets the audit events set.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Gets the connector state set.</summary>
    public DbSet<ConnectorState> ConnectorStates => Set<ConnectorState>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConnectorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
