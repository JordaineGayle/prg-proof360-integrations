using System.Diagnostics;

namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>
/// Propagates the current activity correlation id to FieldFlow as <c>X-Correlation-Id</c>.
/// </summary>
public sealed class FieldFlowCorrelationHandler : DelegatingHandler
{
    /// <summary>Header name (matches API middleware).</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.GetTagItem("correlation.id") as string
                            ?? Activity.Current?.Id
                            ?? Activity.Current?.RootId;

        if (!string.IsNullOrWhiteSpace(correlationId) &&
            !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
