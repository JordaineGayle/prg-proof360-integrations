namespace PRG.Proof360.Integrations.Core.Providers.Health;

/// <summary>
/// Process-local connector runtime signals (circuit, freshness, sanitized last failure).
/// Implemented by the provider adapter; consumed by Application health policy.
/// </summary>
public interface IConnectorRuntimeHealthSource
{
    /// <summary>Provider name (e.g. FieldFlow).</summary>
    string ProviderName { get; }

    /// <summary>Configured provider instance id.</summary>
    string ProviderInstanceId { get; }

    /// <summary>Circuit state: Closed, Open, or HalfOpen.</summary>
    string CircuitState { get; }

    /// <summary>UTC of last successful provider HTTP call.</summary>
    DateTimeOffset? LastSuccessfulProviderCallAt { get; }

    /// <summary>UTC of last sanitized failure.</summary>
    DateTimeOffset? LastFailureAt { get; }

    /// <summary>Sanitized last failure category.</summary>
    string? LastFailureCategory { get; }

    /// <summary>Sanitized last failure code.</summary>
    string? LastFailureCode { get; }

    /// <summary>True when auth/config requires operator attention.</summary>
    bool NeedsAttention { get; }

    /// <summary>Count of rate-limit outcomes within the given window.</summary>
    int CountRecentRateLimits(DateTimeOffset utcNow, TimeSpan window);
}
