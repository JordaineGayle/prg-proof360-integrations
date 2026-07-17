namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>Process-local circuit breaker states exposed on connector health.</summary>
public static class FieldFlowCircuitStates
{
    /// <summary>Normal closed state.</summary>
    public const string Closed = "Closed";

    /// <summary>Failing fast; no HTTP calls.</summary>
    public const string Open = "Open";

    /// <summary>Single controlled probe allowed.</summary>
    public const string HalfOpen = "HalfOpen";
}
