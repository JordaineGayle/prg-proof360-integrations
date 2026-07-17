namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Commits a unit of work spanning canonical and connector infrastructure changes.
/// Does not expose <c>DbSet</c> surfaces to application use cases.
/// </summary>
public interface IConnectorUnitOfWork
{
    /// <summary>
    /// Persists pending changes atomically.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
