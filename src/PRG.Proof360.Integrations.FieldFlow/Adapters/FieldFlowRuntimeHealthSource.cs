using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Health;
using PRG.Proof360.Integrations.FieldFlow.Resilience;

namespace PRG.Proof360.Integrations.FieldFlow.Adapters;

/// <summary>
/// Exposes process-local FieldFlow resilience signals to Application health policy.
/// </summary>
public sealed class FieldFlowRuntimeHealthSource : IConnectorRuntimeHealthSource
{
    private readonly FieldFlowResilienceState _state;
    private readonly FieldFlowOptions _options;

    /// <summary>Creates the source.</summary>
    public FieldFlowRuntimeHealthSource(FieldFlowResilienceState state, IOptions<FieldFlowOptions> options)
    {
        _state = state;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string ProviderName => ProviderNames.FieldFlow;

    /// <inheritdoc />
    public string ProviderInstanceId => _options.ProviderInstanceId;

    /// <inheritdoc />
    public string CircuitState => _state.CircuitState;

    /// <inheritdoc />
    public DateTimeOffset? LastSuccessfulProviderCallAt => _state.LastSuccessfulProviderCallAt;

    /// <inheritdoc />
    public DateTimeOffset? LastFailureAt => _state.LastFailureAt;

    /// <inheritdoc />
    public string? LastFailureCategory => _state.LastFailureCategory;

    /// <inheritdoc />
    public string? LastFailureCode => _state.LastFailureCode;

    /// <inheritdoc />
    public bool NeedsAttention => _state.NeedsAttention;

    /// <inheritdoc />
    public int CountRecentRateLimits(DateTimeOffset utcNow, TimeSpan window) =>
        _state.CountRecentRateLimits(utcNow, window);
}
