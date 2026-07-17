namespace PRG.Proof360.Integrations.Domain;

/// <summary>
/// Marker type for the Proof360 canonical domain assembly.
/// Canonical entities are introduced in later prompts; this assembly must stay free of infrastructure dependencies.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>
    /// Gets the assembly display name used by architecture and composition diagnostics.
    /// </summary>
    public static string Name => typeof(AssemblyMarker).Assembly.GetName().Name ?? "PRG.Proof360.Integrations.Domain";
}
