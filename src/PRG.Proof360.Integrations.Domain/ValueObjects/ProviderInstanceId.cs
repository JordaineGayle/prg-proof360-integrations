namespace PRG.Proof360.Integrations.Domain.ValueObjects;

/// <summary>
/// Identifies a configured provider account/instance. Prevents cross-account external ID collisions.
/// </summary>
public readonly record struct ProviderInstanceId
{
    /// <summary>
    /// Initializes a new <see cref="ProviderInstanceId"/>.
    /// </summary>
    public ProviderInstanceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>Gets the opaque instance identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
