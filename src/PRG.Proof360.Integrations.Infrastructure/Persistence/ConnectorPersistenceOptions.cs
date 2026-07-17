namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// Persistence options for the connector database.
/// </summary>
public sealed class ConnectorPersistenceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ConnectorPersistence";

    /// <summary>SQLite connection string.</summary>
    public string ConnectionString { get; set; } = "Data Source=connector.db";

    /// <summary>
    /// When true, applies migrations (or EnsureCreated fallback) at startup.
    /// Development convenience only — production should run migrations as a controlled release step.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
