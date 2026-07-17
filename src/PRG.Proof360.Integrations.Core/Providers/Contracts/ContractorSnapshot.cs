namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Provider-neutral contractor snapshot for Application processing.
/// Not a Proof360 canonical entity and must not be persisted as one.
/// </summary>
public sealed record ContractorSnapshot
{
    /// <summary>Provider name (for example FieldFlow).</summary>
    public required string ProviderName { get; init; }

    /// <summary>Provider instance id.</summary>
    public required string ProviderInstanceId { get; init; }

    /// <summary>Opaque external contractor id.</summary>
    public required string ExternalContractorId { get; init; }

    /// <summary>Compliance identifier when supplied.</summary>
    public string? ComplianceId { get; init; }

    /// <summary>Whether the provider marks the contractor active.</summary>
    public bool IsActive { get; init; }

    /// <summary>License number.</summary>
    public string? LicenseNumber { get; init; }

    /// <summary>License expiry (date only).</summary>
    public DateOnly? LicenseExpiry { get; init; }

    /// <summary>Insurance policy id.</summary>
    public string? InsurancePolicy { get; init; }

    /// <summary>Insurance expiry.</summary>
    public DateOnly? InsuranceExpiry { get; init; }

    /// <summary>Insurance coverage text.</summary>
    public string? InsuranceCoverage { get; init; }

    /// <summary>WCB number.</summary>
    public string? WcbNumber { get; init; }

    /// <summary>Provider entity version/sequence when known.</summary>
    public long? EntityVersion { get; init; }

    /// <summary>Provider schema version when known.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>
    /// Names of additive optional JSON fields observed but not mapped into this snapshot.
    /// Never copied into canonical storage.
    /// </summary>
    public IReadOnlyList<string> UnknownOptionalFields { get; init; } = [];
}
