namespace PRG.Proof360.Integrations.Domain.ValueObjects;

/// <summary>
/// Opaque provider entity identifier. Case-sensitive unless a provider contract says otherwise.
/// </summary>
public readonly record struct ExternalEntityId
{
    /// <summary>
    /// Initializes a new <see cref="ExternalEntityId"/>.
    /// </summary>
    public ExternalEntityId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque external identifier.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
