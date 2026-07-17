namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Application failure categories used for disposition and HTTP mapping.
/// Independent of HTTP status codes in Domain/Application logic.
/// </summary>
public enum FailureCategory
{
    /// <summary>Input or structural validation failure.</summary>
    Validation = 0,

    /// <summary>Canonical or identity record not found.</summary>
    NotFound,

    /// <summary>Conflict / invalid transition / idempotency clash.</summary>
    Conflict,

    /// <summary>Approval or eligibility rejection.</summary>
    Approval,

    /// <summary>Missing dependency (for example contractor identity).</summary>
    Dependency,

    /// <summary>Provider schema/contract violation.</summary>
    ProviderContract,

    /// <summary>Provider authentication/configuration failure.</summary>
    ProviderAuthentication,

    /// <summary>Inbound caller authentication failure (e.g. invalid webhook signature).</summary>
    Unauthorized,

    /// <summary>Provider rate limited.</summary>
    RateLimited,

    /// <summary>Provider timeout / ambiguous transport.</summary>
    Timeout,

    /// <summary>Provider unavailable or circuit open.</summary>
    Unavailable,

    /// <summary>Persistence uniqueness/claim conflict.</summary>
    PersistenceConflict,

    /// <summary>Unexpected sanitized failure at the outer boundary.</summary>
    Unexpected
}
