namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Discrete provider capabilities. Future providers declare support without throwing
/// <see cref="NotImplementedException"/> during normal use.
/// </summary>
[Flags]
public enum ProviderCapability
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>List/read contractor snapshots.</summary>
    ContractorSnapshots = 1,

    /// <summary>List/read work-order snapshots.</summary>
    WorkOrderSnapshots = 2,

    /// <summary>Dispatch (create) work orders outbound.</summary>
    WorkOrderDispatch = 4,

    /// <summary>Reconcile/read work-order status by external id or client reference.</summary>
    WorkOrderReconcile = 8,

    /// <summary>Verify inbound webhook authenticity.</summary>
    WebhookVerification = 16
}
