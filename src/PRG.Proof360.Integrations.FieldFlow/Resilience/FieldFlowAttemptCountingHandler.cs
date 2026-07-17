namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>
/// Innermost handler that counts real HTTP attempts after resilience retries/timeouts.
/// </summary>
public sealed class FieldFlowAttemptCountingHandler : DelegatingHandler
{
    private readonly FieldFlowResilienceState _state;

    /// <summary>Creates the handler.</summary>
    public FieldFlowAttemptCountingHandler(FieldFlowResilienceState state)
    {
        _state = state;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _state.RecordHttpAttempt();
        return base.SendAsync(request, cancellationToken);
    }
}
