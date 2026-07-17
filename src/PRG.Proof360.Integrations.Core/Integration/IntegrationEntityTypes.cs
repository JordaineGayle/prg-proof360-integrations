namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Stable external entity type names used in identity links and synthetic poll event ids.
/// </summary>
public static class ExternalEntityTypes
{
    /// <summary>FieldFlow contractor.</summary>
    public const string Contractor = "contractor";

    /// <summary>FieldFlow work order.</summary>
    public const string WorkOrder = "work_order";
}

/// <summary>
/// Stable canonical entity type names stored on identity links and audit rows.
/// </summary>
public static class CanonicalEntityTypes
{
    /// <summary>Vendor.</summary>
    public const string Vendor = "Vendor";

    /// <summary>Job.</summary>
    public const string Job = "Job";
}

/// <summary>
/// Inbox event types for inbound sync. Polling and webhooks share these application paths.
/// </summary>
public static class InboxEventTypes
{
    /// <summary>Contractor snapshot (poll or webhook-normalized).</summary>
    public const string ContractorSnapshot = "contractor.snapshot";

    /// <summary>Work-order snapshot (poll or webhook-normalized).</summary>
    public const string WorkOrderSnapshot = "work_order.snapshot";

    /// <summary>FieldFlow work-order status webhook (envelope is still a WorkOrderSnapshot JSON).</summary>
    public const string WorkOrderStatusChanged = "work_order.status_changed";
}

/// <summary>
/// Supported FieldFlow webhook schema versions for Prompt 06.
/// </summary>
public static class WebhookSchemaVersions
{
    /// <summary>Current supported schema.</summary>
    public const string V1 = "1.0";
}

/// <summary>
/// Outbox operation type names.
/// </summary>
public static class OutboxOperationTypes
{
    /// <summary>Qualified Job → FieldFlow work-order create.</summary>
    public const string DispatchWorkOrder = "work_order.dispatch";
}

/// <summary>
/// Stable outbound idempotency key helpers.
/// </summary>
public static class DispatchIdempotencyKeys
{
    /// <summary>
    /// Format: <c>fieldflow:{providerInstance}:{jobId}:dispatch:v1</c>.
    /// Never regenerate for the same logical dispatch.
    /// </summary>
    public static string ForJob(string providerInstanceId, Guid jobId) =>
        $"fieldflow:{providerInstanceId}:{jobId:D}:dispatch:v1";
}
