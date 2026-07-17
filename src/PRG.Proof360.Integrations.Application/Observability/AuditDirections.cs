namespace PRG.Proof360.Integrations.Application.Observability;

/// <summary>Audit direction values.</summary>
public static class AuditDirections
{
    /// <summary>Inbound provider → Proof360.</summary>
    public const string Inbound = "inbound";

    /// <summary>Outbound Proof360 → provider.</summary>
    public const string Outbound = "outbound";

    /// <summary>Internal / ops.</summary>
    public const string Internal = "internal";
}
