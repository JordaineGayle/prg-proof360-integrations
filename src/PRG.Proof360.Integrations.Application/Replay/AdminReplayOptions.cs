namespace PRG.Proof360.Integrations.Application.Replay;

/// <summary>
/// Prototype admin replay controls. Production must require authz/approval (documented in runbook).
/// </summary>
public sealed class AdminReplayOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "AdminReplay";

    /// <summary>
    /// When true, local admin replay endpoints are mapped.
    /// Default false; Development host enables via appsettings.Development.json.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional shared operator token for the prototype. Empty = Development-only host check.
    /// </summary>
    public string? OperatorToken { get; set; }
}
