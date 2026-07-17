using System.Text.Json.Serialization;

namespace PRG.FieldFlow.Mock.Models;

/// <summary>
/// Mock-only contractor JSON shape. Intentionally not shared with the connector FieldFlow adapter project
/// so HTTP fixtures detect contract drift instead of accidental compile-time coupling.
/// </summary>
public sealed class ContractorDto
{
    /// <summary>Opaque FieldFlow contractor identifier.</summary>
    [JsonPropertyName("contractorId")]
    public string ContractorId { get; set; } = string.Empty;

    /// <summary>Optional compliance identifier.</summary>
    [JsonPropertyName("complianceId")]
    public string? ComplianceId { get; set; }

    /// <summary>Whether the contractor is active at the provider.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    /// <summary>License details.</summary>
    [JsonPropertyName("license")]
    public LicenseDto? License { get; set; }

    /// <summary>Insurance details.</summary>
    [JsonPropertyName("insurance")]
    public InsuranceDto? Insurance { get; set; }

    /// <summary>Workers compensation board number.</summary>
    [JsonPropertyName("wcbNumber")]
    public string? WcbNumber { get; set; }

    /// <summary>Display name for fixtures only.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>License nested object.</summary>
public sealed class LicenseDto
{
    /// <summary>License number.</summary>
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    /// <summary>Expiry date (yyyy-MM-dd).</summary>
    [JsonPropertyName("expiresOn")]
    public string? ExpiresOn { get; set; }
}

/// <summary>Insurance nested object.</summary>
public sealed class InsuranceDto
{
    /// <summary>Policy number.</summary>
    [JsonPropertyName("policy")]
    public string? Policy { get; set; }

    /// <summary>Expiry date (yyyy-MM-dd).</summary>
    [JsonPropertyName("expiresOn")]
    public string? ExpiresOn { get; set; }

    /// <summary>Coverage description/limit text.</summary>
    [JsonPropertyName("coverage")]
    public string? Coverage { get; set; }
}
