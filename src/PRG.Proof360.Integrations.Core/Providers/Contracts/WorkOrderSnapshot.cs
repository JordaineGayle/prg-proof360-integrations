namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Provider-neutral work-order snapshot for Application processing.
/// Not a Proof360 canonical entity and must not be persisted as one.
/// </summary>
public sealed record WorkOrderSnapshot
{
    /// <summary>Provider name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Provider instance id.</summary>
    public required string ProviderInstanceId { get; init; }

    /// <summary>Opaque external work-order id.</summary>
    public required string ExternalWorkOrderId { get; init; }

    /// <summary>Proof360 Job ID when used as client reference.</summary>
    public string? ClientReference { get; init; }

    /// <summary>Opaque external contractor id when present.</summary>
    public string? ExternalContractorId { get; init; }

    /// <summary>Raw provider operational status string.</summary>
    public required string ProviderStatus { get; init; }

    /// <summary>Provider entity version/sequence.</summary>
    public long EntityVersion { get; init; }

    /// <summary>Provider schema version when known.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Customer name.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Customer phone.</summary>
    public string? CustomerPhone { get; init; }

    /// <summary>Customer email.</summary>
    public string? CustomerEmail { get; init; }

    /// <summary>Street.</summary>
    public string? AddressStreet { get; init; }

    /// <summary>Unit.</summary>
    public string? AddressUnit { get; init; }

    /// <summary>City.</summary>
    public string? AddressCity { get; init; }

    /// <summary>Postal.</summary>
    public string? AddressPostal { get; init; }

    /// <summary>Service type.</summary>
    public string? ServiceType { get; init; }

    /// <summary>Subcategory.</summary>
    public string? Subcategory { get; init; }

    /// <summary>Desired window start (UTC). Not appointment actuals.</summary>
    public DateTimeOffset? WindowStart { get; init; }

    /// <summary>Desired window end (UTC).</summary>
    public DateTimeOffset? WindowEnd { get; init; }

    /// <summary>Provider notes with defined semantics only.</summary>
    public string? Notes { get; init; }

    /// <summary>Event occurred-at when sourced from a webhook envelope.</summary>
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>Additive optional JSON field names observed but not mapped.</summary>
    public IReadOnlyList<string> UnknownOptionalFields { get; init; } = [];
}
