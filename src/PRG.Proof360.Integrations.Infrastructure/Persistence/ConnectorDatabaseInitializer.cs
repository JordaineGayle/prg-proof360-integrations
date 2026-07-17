using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// Development-oriented database initializer. Production should apply migrations outside request traffic.
/// </summary>
internal sealed class ConnectorDatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ConnectorPersistenceOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ConnectorDatabaseInitializer> _logger;

    public ConnectorDatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        IOptions<ConnectorPersistenceOptions> options,
        IHostEnvironment environment,
        ILogger<ConnectorDatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "ApplyMigrationsOnStartup is enabled outside Development. Prefer controlled release migrations in production.");
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConnectorDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Connector database migrations applied.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
