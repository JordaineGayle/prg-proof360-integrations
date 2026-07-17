using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;

namespace PRG.Proof360.Integrations.FieldFlow;

/// <summary>
/// Declares FieldFlow Phase 1 capabilities for discovery.
/// </summary>
public sealed class FieldFlowProviderCapabilities : IProviderCapabilities
{
    private readonly FieldFlowOptions _options;

    /// <summary>Creates the descriptor.</summary>
    public FieldFlowProviderCapabilities(IOptions<FieldFlowOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderName => ProviderNames.FieldFlow;

    /// <inheritdoc />
    public string ProviderInstanceId => _options.ProviderInstanceId;

    /// <inheritdoc />
    public ProviderCapability SupportedCapabilities =>
        ProviderCapability.ContractorSnapshots |
        ProviderCapability.WorkOrderSnapshots |
        ProviderCapability.WorkOrderDispatch |
        ProviderCapability.WorkOrderReconcile |
        ProviderCapability.WebhookVerification;

    /// <inheritdoc />
    public bool Supports(ProviderCapability capability) =>
        capability != ProviderCapability.None && SupportedCapabilities.HasFlag(capability);
}
