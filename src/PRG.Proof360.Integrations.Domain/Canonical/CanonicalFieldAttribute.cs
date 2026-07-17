namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Marks a property as a fixed Proof360 canonical field whose external/database name must match the assignment exactly.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class CanonicalFieldAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CanonicalFieldAttribute"/> class.
    /// </summary>
    /// <param name="name">Exact assignment field name, including required capitalization.</param>
    public CanonicalFieldAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>
    /// Gets the exact canonical field name required by the assignment.
    /// </summary>
    public string Name { get; }
}
