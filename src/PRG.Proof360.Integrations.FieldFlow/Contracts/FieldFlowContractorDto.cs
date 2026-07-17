using System.Text.Json;
using System.Text.Json.Serialization;

namespace PRG.Proof360.Integrations.FieldFlow.Contracts;

/// <summary>
/// FieldFlow contractor JSON DTO matching the mock contract only.
/// Intentionally not shared with <c>PRG.FieldFlow.Mock</c>.
/// </summary>
public sealed class FieldFlowContractorDto
{
    /// <summary>Opaque contractor id (required).</summary>
    [JsonPropertyName("contractorId")]
    public string? ContractorId { get; set; }

    /// <summary>Compliance id.</summary>
    [JsonPropertyName("complianceId")]
    public string? ComplianceId { get; set; }

    /// <summary>Active flag.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>Display name (no canonical mapping; ignored).</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>License object.</summary>
    [JsonPropertyName("license")]
    public FieldFlowLicenseDto? License { get; set; }

    /// <summary>Insurance object.</summary>
    [JsonPropertyName("insurance")]
    public FieldFlowInsuranceDto? Insurance { get; set; }

    /// <summary>WCB number.</summary>
    [JsonPropertyName("wcbNumber")]
    public string? WcbNumber { get; set; }

    /// <summary>Additive unknown optional fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>License nested DTO.</summary>
public sealed class FieldFlowLicenseDto
{
    /// <summary>License number.</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    /// <summary>Expiry yyyy-MM-dd.</summary>
    [JsonPropertyName("expiresOn")]
    public string? ExpiresOn { get; set; }
}

/// <summary>Insurance nested DTO.</summary>
public sealed class FieldFlowInsuranceDto
{
    /// <summary>Policy number.</summary>
    [JsonPropertyName("policy")]
    public string? Policy { get; set; }

    /// <summary>Expiry yyyy-MM-dd.</summary>
    [JsonPropertyName("expiresOn")]
    public string? ExpiresOn { get; set; }

    /// <summary>Coverage text.</summary>
    [JsonPropertyName("coverage")]
    public string? Coverage { get; set; }
}
