namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Capability discovery for a configured provider adapter.
/// </summary>
public interface IProviderCapabilities
{
    /// <summary>Stable provider name (for example <c>FieldFlow</c>).</summary>
    string ProviderName { get; }

    /// <summary>Configured provider instance identifier.</summary>
    string ProviderInstanceId { get; }

    /// <summary>Returns whether the named capability is supported.</summary>
    bool Supports(ProviderCapability capability);

    /// <summary>Supported capability flags.</summary>
    ProviderCapability SupportedCapabilities { get; }
}
