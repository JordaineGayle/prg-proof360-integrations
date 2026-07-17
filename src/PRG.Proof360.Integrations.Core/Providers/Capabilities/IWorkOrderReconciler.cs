using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Reads current work-order state for reconciliation after ambiguous outcomes.
/// </summary>
public interface IWorkOrderReconciler
{
    /// <summary>Gets current state by external work-order id.</summary>
    Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByExternalIdAsync(
        string externalWorkOrderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds a work order by Proof360 client reference when the provider supports it.
    /// Unsupported providers return <see cref="ProviderFailure.Unsupported"/>.
    /// </summary>
    Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByClientReferenceAsync(
        string clientReference,
        CancellationToken cancellationToken);
}
