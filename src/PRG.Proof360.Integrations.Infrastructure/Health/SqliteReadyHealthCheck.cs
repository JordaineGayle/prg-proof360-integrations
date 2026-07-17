using Microsoft.Extensions.Diagnostics.HealthChecks;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.Infrastructure.Health;

/// <summary>
/// Readiness check for local SQLite. FieldFlow availability is intentionally excluded.
/// </summary>
public sealed class SqliteReadyHealthCheck : IHealthCheck
{
    private readonly ConnectorDbContext _dbContext;

    /// <summary>Creates the check.</summary>
    public SqliteReadyHealthCheck(ConnectorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("SQLite is reachable.")
            : HealthCheckResult.Unhealthy("SQLite is not reachable.");
    }
}
