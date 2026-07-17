namespace PRG.Proof360.Integrations.Domain.ValueObjects;

/// <summary>
/// Correlates a logical operation across inbox, outbox, audit, and logs.
/// </summary>
public readonly record struct CorrelationId
{
    /// <summary>
    /// Initializes a new <see cref="CorrelationId"/>.
    /// </summary>
    public CorrelationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>Creates a new random correlation identifier.</summary>
    public static CorrelationId NewId() => new(Guid.NewGuid().ToString("N"));

    /// <summary>Gets the correlation identifier value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
