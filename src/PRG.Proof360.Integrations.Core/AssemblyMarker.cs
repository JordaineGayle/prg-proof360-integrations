namespace PRG.Proof360.Integrations.Core;

/// <summary>
/// Marker type for provider-neutral connector contracts and integration metadata models.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>
    /// Gets the assembly display name used by architecture and composition diagnostics.
    /// </summary>
    public static string Name => typeof(AssemblyMarker).Assembly.GetName().Name ?? "PRG.Proof360.Integrations.Core";
}
