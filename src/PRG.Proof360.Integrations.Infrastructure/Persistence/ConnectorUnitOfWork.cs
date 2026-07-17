using PRG.Proof360.Integrations.Application.Abstractions.Persistence;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// EF-backed unit of work.
/// </summary>
internal sealed class ConnectorUnitOfWork : IConnectorUnitOfWork
{
    private readonly ConnectorDbContext _dbContext;

    public ConnectorUnitOfWork(ConnectorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public void ClearTrackedChanges() => _dbContext.ChangeTracker.Clear();
}
