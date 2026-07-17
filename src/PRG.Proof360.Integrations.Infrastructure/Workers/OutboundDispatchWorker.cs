using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;

namespace PRG.Proof360.Integrations.Infrastructure.Workers;

/// <summary>
/// In-process outbox worker. HTTP outside DB TX. Prompt 08 owns nested HTTP resilience.
/// </summary>
public sealed class OutboundDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OutboundDispatchOptions> _options;
    private readonly ILogger<OutboundDispatchWorker> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the worker.</summary>
    public OutboundDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboundDispatchOptions> options,
        ILogger<OutboundDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _logger.LogInformation("Outbound dispatch worker is disabled.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.Value.WorkerIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbound outbox cycle failed unexpectedly.");
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

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var process = scope.ServiceProvider.GetRequiredService<ProcessOutboxMessageHandler>();
            var capabilities = scope.ServiceProvider.GetRequiredService<IProviderCapabilities>();
            var max = Math.Max(1, _options.Value.MaxProcessBatch);

            for (var i = 0; i < max; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await process.HandleAsync(capabilities.ProviderInstanceId, cancellationToken);
                if (result.IsSuccess &&
                    ((Core.Results.Result<ProcessOutboxOutcome, Application.Errors.IntegrationFailure>.Succeeded)result)
                    .Value is ProcessOutboxOutcome.Idle)
                {
                    break;
                }
            }
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
