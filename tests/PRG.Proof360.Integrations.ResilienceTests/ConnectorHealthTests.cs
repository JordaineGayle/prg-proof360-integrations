using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Health;
using PRG.Proof360.Integrations.Core.Providers.Health;
using PRG.Proof360.Integrations.FieldFlow.Resilience;

namespace PRG.Proof360.Integrations.ResilienceTests;

public sealed class ConnectorHealthTests
{
    [Theory]
    [InlineData(false, "Closed", null, null, 0, 0, 0, ConnectorHealthStatuses.Healthy)]
    [InlineData(false, "HalfOpen", null, null, 0, 0, 0, ConnectorHealthStatuses.Degraded)]
    [InlineData(false, "Open", null, null, 0, 0, 0, ConnectorHealthStatuses.Offline)]
    [InlineData(true, "Closed", null, null, 0, 0, 0, ConnectorHealthStatuses.NeedsAttention)]
    [InlineData(false, "Closed", null, null, 0, 0, 1, ConnectorHealthStatuses.Degraded)]
    public void Health_status_transitions_are_deterministic(
        bool needsAttention,
        string circuit,
        DateTimeOffset? lastSuccess,
        DateTimeOffset? lastFailure,
        int inbox,
        int outbox,
        int deadLetters,
        string expected)
    {
        var runtime = new FakeRuntime
        {
            NeedsAttention = needsAttention,
            CircuitState = circuit,
            LastSuccessfulProviderCallAt = lastSuccess,
            LastFailureAt = lastFailure,
            LastFailureCategory = needsAttention ? "Authentication" : lastFailure is null ? null : "Unavailable"
        };
        var policy = new ConnectorHealthStatusPolicy(Options.Create(new ConnectorHealthOptions
        {
            DegradedInboxBacklogThreshold = 50,
            DegradedOutboxBacklogThreshold = 50,
            DegradedRateLimitThreshold = 3,
            OfflineNoSuccessSeconds = 300
        }));

        var status = policy.ResolveStatus(
            runtime,
            new IntegrationBacklogMetrics(inbox, outbox, deadLetters, 0, null),
            recentRateLimitCount: 0,
            utcNow: DateTimeOffset.Parse("2026-07-17T12:00:00Z"));

        Assert.Equal(expected, status);
    }

    [Fact]
    public void Health_snapshot_contains_no_sensitive_markers()
    {
        var runtime = new FakeRuntime
        {
            CircuitState = FieldFlowCircuitStates.Closed,
            LastFailureCategory = "Unavailable",
            LastFailureCode = "server_error",
            LastFailureAt = DateTimeOffset.UtcNow
        };
        var policy = new ConnectorHealthStatusPolicy(Options.Create(new ConnectorHealthOptions()));
        var snapshot = policy.Evaluate(
            runtime,
            new IntegrationBacklogMetrics(1, 0, 0, 0, DateTimeOffset.UtcNow.AddMinutes(-1)),
            lastSuccessfulSyncAt: DateTimeOffset.UtcNow,
            utcNow: DateTimeOffset.UtcNow);

        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        foreach (var marker in new[]
                 {
                     "replace-me", "X-Api-Key", "password", "Bearer ", "@example.com", "+1-555", "webhook_hmac"
                 })
        {
            Assert.DoesNotContain(marker, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class FakeRuntime : IConnectorRuntimeHealthSource
    {
        public string ProviderName => "FieldFlow";
        public string ProviderInstanceId => "fieldflow-resilience-1";
        public string CircuitState { get; init; } = "Closed";
        public DateTimeOffset? LastSuccessfulProviderCallAt { get; init; }
        public DateTimeOffset? LastFailureAt { get; init; }
        public string? LastFailureCategory { get; init; }
        public string? LastFailureCode { get; init; }
        public bool NeedsAttention { get; init; }
        public int CountRecentRateLimits(DateTimeOffset utcNow, TimeSpan window) => 0;
    }
}
