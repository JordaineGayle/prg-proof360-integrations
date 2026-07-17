using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Reads work-order snapshots from a field-service provider.
/// </summary>
public interface IWorkOrderSnapshotSource
{
    /// <summary>Lists work-order snapshots.</summary>
    Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets a work-order snapshot by external id.</summary>
    Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken);
}
