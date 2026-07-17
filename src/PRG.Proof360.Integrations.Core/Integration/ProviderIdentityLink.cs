namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Sidecar lineage between a provider external entity and a Proof360 canonical entity.
/// Not a canonical Proof360 entity. Uniqueness is enforced in the database because concurrent
/// imports can race past in-memory pre-checks.
/// </summary>
public sealed class ProviderIdentityLink
{
    /// <summary>Gets or sets Id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets ProviderName.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string ProviderInstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets ExternalEntityType.</summary>
    public string ExternalEntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets ExternalId.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Gets or sets CanonicalEntityType.</summary>
    public string CanonicalEntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets CanonicalId.</summary>
    public Guid CanonicalId { get; set; }

    /// <summary>Gets or sets MatchKey.</summary>
    public string? MatchKey { get; set; }

    /// <summary>Gets or sets LastAppliedVersion.</summary>
    public long? LastAppliedVersion { get; set; }

    /// <summary>Gets or sets LastAppliedAt.</summary>
    public DateTimeOffset? LastAppliedAt { get; set; }

    /// <summary>Gets or sets PayloadHash.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>Optimistic concurrency token for claim/update races under SQLite.</summary>
    public uint RowVersion { get; set; }
}
