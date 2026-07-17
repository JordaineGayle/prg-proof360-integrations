namespace PRG.Proof360.Integrations.Application.Mapping;

/// <summary>
/// Canonical Job <c>source</c> markers used by origin-dependent ownership.
/// </summary>
public static class JobSources
{
    /// <summary>Job originated in Proof360 (outbound dispatch path).</summary>
    public const string Proof360 = "Proof360";

    /// <summary>Job originated from a FieldFlow inbound import.</summary>
    public const string FieldFlow = "FieldFlow";
}
