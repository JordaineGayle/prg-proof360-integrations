using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.WorkOrders;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;

namespace PRG.Proof360.Integrations.Infrastructure.Workers;

/// <summary>
/// In-process polling worker. Contractors before work orders. One poll at a time per process.
/// Distributed locking is deferred to production.
/// </summary>
public sealed class InboundPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<InboundSyncOptions> _options;
    private readonly ILogger<InboundPollingWorker> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the worker.</summary>
    public InboundPollingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<InboundSyncOptions> options,
        ILogger<InboundPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.PollingEnabled)
        {
            _logger.LogInformation("Inbound polling worker is disabled.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(5, _options.Value.PollingIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inbound poll cycle failed unexpectedly.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            _logger.LogDebug("Skipping overlapping inbound poll in this process.");
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var contractors = scope.ServiceProvider.GetRequiredService<ImportContractorsHandler>();
            var workOrders = scope.ServiceProvider.GetRequiredService<ImportWorkOrdersHandler>();
            var capabilities = scope.ServiceProvider.GetRequiredService<IProviderCapabilities>();
            var store = scope.ServiceProvider.GetRequiredService<IIntegrationStore>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IConnectorUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var contractorResult = await contractors.HandleAsync(cancellationToken);
            if (contractorResult.IsFailure)
            {
                _logger.LogWarning(
                    "Contractor import failed: {Code}",
                    ((Core.Results.Result<Application.Outcomes.ImportContractorsOutcome, Application.Errors.IntegrationFailure>.Failed)contractorResult).Error.Code);
            }

            var workOrderResult = await workOrders.HandleAsync(cancellationToken);
            if (workOrderResult.IsFailure)
            {
                _logger.LogWarning(
                    "Work-order import failed: {Code}",
                    ((Core.Results.Result<ImportWorkOrdersOutcome, Application.Errors.IntegrationFailure>.Failed)workOrderResult).Error.Code);
            }

            // Checkpoint only after the batch was durably accepted into inbox/process paths above.
            // Preserve circuit/error projection fields owned by the health/resilience path.
            var existing = await store.FindConnectorStateAsync(
                ProviderNames.FieldFlow,
                capabilities.ProviderInstanceId,
                cancellationToken);
            await store.UpsertConnectorStateAsync(
                new ConnectorState
                {
                    Id = existing?.Id ?? Guid.NewGuid(),
                    ProviderName = ProviderNames.FieldFlow,
                    ProviderInstanceId = capabilities.ProviderInstanceId,
                    LastSuccessfulSyncAt = clock.UtcNow,
                    LastCheckpoint = $"poll:{clock.UtcNow:O}",
                    CircuitState = existing?.CircuitState ?? "Closed",
                    InboxBacklogCount = existing?.InboxBacklogCount ?? 0,
                    OutboxBacklogCount = existing?.OutboxBacklogCount ?? 0,
                    DeadLetterCount = existing?.DeadLetterCount ?? 0,
                    LastErrorCategory = existing?.LastErrorCategory,
                    LastErrorMessage = existing?.LastErrorMessage,
                    UpdatedAt = clock.UtcNow
                },
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }
}
