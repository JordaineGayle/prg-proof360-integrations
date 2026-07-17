namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Provider-neutral outbound work-order create command.
/// </summary>
public sealed class DispatchWorkOrderCommand
{
    /// <summary>Idempotency key for the provider create call.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Proof360 Job ID as client reference.</summary>
    public required string ClientReference { get; init; }

    /// <summary>External contractor id when known.</summary>
    public string? ExternalContractorId { get; init; }

    /// <summary>Customer name.</summary>
    public required string CustomerName { get; init; }

    /// <summary>Customer phone.</summary>
    public string? CustomerPhone { get; init; }

    /// <summary>Customer email.</summary>
    public string? CustomerEmail { get; init; }

    /// <summary>Street.</summary>
    public required string AddressStreet { get; init; }

    /// <summary>Unit.</summary>
    public string? AddressUnit { get; init; }

    /// <summary>City.</summary>
    public required string AddressCity { get; init; }

    /// <summary>Postal.</summary>
    public string? AddressPostal { get; init; }

    /// <summary>Service type.</summary>
    public required string ServiceType { get; init; }

    /// <summary>Subcategory.</summary>
    public string? Subcategory { get; init; }

    /// <summary>Desired window start.</summary>
    public DateTimeOffset? WindowStart { get; init; }

    /// <summary>Desired window end.</summary>
    public DateTimeOffset? WindowEnd { get; init; }

    /// <summary>Notes.</summary>
    public string? Notes { get; init; }
}
