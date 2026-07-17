using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Reads contractor snapshots from a field-service provider.
/// </summary>
public interface IContractorSnapshotSource
{
    /// <summary>Lists contractor snapshots.</summary>
    Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Gets a single contractor snapshot by external id when supported.</summary>
    Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(string externalContractorId, CancellationToken cancellationToken);
}
