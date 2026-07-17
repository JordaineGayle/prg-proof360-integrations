using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Dispatches (creates) work orders at a field-service provider.
/// </summary>
public interface IWorkOrderDispatcher
{
    /// <summary>Creates a work order using provider idempotency semantics.</summary>
    Task<Result<WorkOrderSnapshot, ProviderFailure>> DispatchAsync(
        DispatchWorkOrderCommand command,
        CancellationToken cancellationToken);
}
