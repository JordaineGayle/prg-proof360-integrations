namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Provider-port operational failure kinds. Independent of HTTP and FieldFlow DTOs.
/// </summary>
public enum ProviderFailureKind
{
    /// <summary>Provider or adapter validation failure.</summary>
    Validation = 0,

    /// <summary>Authentication failed.</summary>
    Authentication,

    /// <summary>Authenticated but forbidden.</summary>
    Forbidden,

    /// <summary>Rate limited by the provider.</summary>
    RateLimited,

    /// <summary>Timeout / cancelled transport after caller token still active.</summary>
    Timeout,

    /// <summary>Provider unavailable.</summary>
    Unavailable,

    /// <summary>Circuit breaker open (Prompt 08+).</summary>
    CircuitOpen,

    /// <summary>Contract/schema/unsupported capability violation.</summary>
    ContractViolation,

    /// <summary>Write may have succeeded but response was lost/ambiguous.</summary>
    AmbiguousWrite,

    /// <summary>Resource not found at the provider.</summary>
    NotFound,

    /// <summary>Conflict such as idempotency key reuse.</summary>
    Conflict
}
