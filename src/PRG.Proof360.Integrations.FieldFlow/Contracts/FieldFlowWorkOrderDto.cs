using System.Text.Json;
using System.Text.Json.Serialization;

namespace PRG.Proof360.Integrations.FieldFlow.Contracts;

/// <summary>
/// FieldFlow work-order JSON DTO matching the mock contract only.
/// </summary>
public sealed class FieldFlowWorkOrderDto
{
    /// <summary>Opaque work-order id (required).</summary>
    [JsonPropertyName("workOrderId")]
    public string? WorkOrderId { get; set; }

    /// <summary>Opaque contractor id.</summary>
    [JsonPropertyName("contractorId")]
    public string? ContractorId { get; set; }

    /// <summary>Proof360 Job ID client reference.</summary>
    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; set; }

    /// <summary>Provider status.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Entity version/sequence.</summary>
    [JsonPropertyName("entityVersion")]
    public long EntityVersion { get; set; }

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

    /// <summary>Postal.</summary>
    [JsonPropertyName("addressPostal")]
    public string? AddressPostal { get; set; }

    /// <summary>Service type.</summary>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }

    /// <summary>Subcategory.</summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>Desired window start.</summary>
    [JsonPropertyName("windowStart")]
    public DateTimeOffset? WindowStart { get; set; }

    /// <summary>Desired window end.</summary>
    [JsonPropertyName("windowEnd")]
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>Notes.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    /// <summary>Additive unknown optional fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Create work-order request body.</summary>
public sealed class FieldFlowCreateWorkOrderRequestDto
{
    /// <summary>Client reference.</summary>
    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; set; }

    /// <summary>Contractor id.</summary>
    [JsonPropertyName("contractorId")]
    public string? ContractorId { get; set; }

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

    /// <summary>Postal.</summary>
    [JsonPropertyName("addressPostal")]
    public string? AddressPostal { get; set; }

    /// <summary>Service type.</summary>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }

    /// <summary>Subcategory.</summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>Window start.</summary>
    [JsonPropertyName("windowStart")]
    public DateTimeOffset? WindowStart { get; set; }

    /// <summary>Window end.</summary>
    [JsonPropertyName("windowEnd")]
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>Notes.</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>Stable FieldFlow error body.</summary>
public sealed class FieldFlowErrorDto
{
    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>Message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
