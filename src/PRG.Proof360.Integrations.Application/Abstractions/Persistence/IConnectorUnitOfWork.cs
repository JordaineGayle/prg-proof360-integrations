namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Unit of work for connector persistence.
/// </summary>
public interface IConnectorUnitOfWork
{
    /// <summary>Persists staged changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards tracked entity changes after a failed apply so disposition can update inbox alone.
    /// </summary>
    void ClearTrackedChanges();
}
