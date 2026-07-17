using System.Text.Json;
using System.Text.Json.Serialization;

namespace PRG.FieldFlow.Mock.Models;

/// <summary>
/// Mock-only work-order JSON shape. Not shared with connector adapter assemblies.
/// </summary>
public sealed class WorkOrderDto
{
    /// <summary>Opaque FieldFlow work-order identifier.</summary>
    [JsonPropertyName("workOrderId")]
    public string WorkOrderId { get; set; } = string.Empty;

    /// <summary>Opaque contractor identifier.</summary>
    [JsonPropertyName("contractorId")]
    public string? ContractorId { get; set; }

    /// <summary>Proof360 Job ID used as client reference for reconciliation.</summary>
    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; set; }

    /// <summary>Provider status: open, scheduled, in_progress, done, void.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = WorkOrderStatuses.Open;

    /// <summary>Monotonic entity version/sequence for ordering tests.</summary>
    [JsonPropertyName("entityVersion")]
    public long EntityVersion { get; set; } = 1;

    /// <summary>Customer name.</summary>
    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    /// <summary>Customer phone.</summary>
    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    /// <summary>Customer email.</summary>
    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    /// <summary>Street.</summary>
    [JsonPropertyName("addressStreet")]
    public string? AddressStreet { get; set; }

    /// <summary>Unit.</summary>
    [JsonPropertyName("addressUnit")]
    public string? AddressUnit { get; set; }

    /// <summary>City.</summary>
    [JsonPropertyName("addressCity")]
    public string? AddressCity { get; set; }

    /// <summary>Postal code.</summary>
    [JsonPropertyName("addressPostal")]
    public string? AddressPostal { get; set; }

    /// <summary>Service type.</summary>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }

    /// <summary>Subcategory.</summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>Desired window start (UTC ISO-8601).</summary>
    [JsonPropertyName("windowStart")]
    public DateTimeOffset? WindowStart { get; set; }

    /// <summary>Desired window end (UTC ISO-8601).</summary>
    [JsonPropertyName("windowEnd")]
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>Notes.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// Extension data for additive unknown optional fields (schema-evolution fixture).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Assumed FieldFlow work-order statuses.</summary>
public static class WorkOrderStatuses
{
    /// <summary>Newly opened / dispatched equivalent.</summary>
    public const string Open = "open";

    /// <summary>Scheduled.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>In progress.</summary>
    public const string InProgress = "in_progress";

    /// <summary>Completed.</summary>
    public const string Done = "done";

    /// <summary>Cancelled/void.</summary>
    public const string Void = "void";
}

/// <summary>Create work-order request body.</summary>
public sealed class CreateWorkOrderRequest
{
    /// <summary>Proof360 Job ID client reference (required).</summary>
    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; set; }

    /// <summary>Contractor to assign when known.</summary>
    [JsonPropertyName("contractorId")]
    public string? ContractorId { get; set; }

    /// <summary>Customer name (required).</summary>
    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    /// <summary>Customer phone.</summary>
    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    /// <summary>Customer email.</summary>
    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    /// <summary>Street (required).</summary>
    [JsonPropertyName("addressStreet")]
    public string? AddressStreet { get; set; }

    /// <summary>Unit.</summary>
    [JsonPropertyName("addressUnit")]
    public string? AddressUnit { get; set; }

    /// <summary>City (required).</summary>
    [JsonPropertyName("addressCity")]
    public string? AddressCity { get; set; }

    /// <summary>Postal.</summary>
    [JsonPropertyName("addressPostal")]
    public string? AddressPostal { get; set; }

    /// <summary>Service type (required).</summary>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }

    /// <summary>Subcategory.</summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>Window start UTC.</summary>
    [JsonPropertyName("windowStart")]
    public DateTimeOffset? WindowStart { get; set; }

    /// <summary>Window end UTC.</summary>
    [JsonPropertyName("windowEnd")]
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>Notes.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>Status patch request.</summary>
public sealed class PatchStatusRequest
{
    /// <summary>New status.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Optional explicit entity version; otherwise mock increments.</summary>
    [JsonPropertyName("entityVersion")]
    public long? EntityVersion { get; set; }
}
