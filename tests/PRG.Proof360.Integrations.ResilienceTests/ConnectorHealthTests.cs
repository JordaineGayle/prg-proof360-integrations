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
    public void Prolonged_silence_after_failure_is_Offline()
    {
        var utcNow = DateTimeOffset.Parse("2026-07-17T12:00:00Z");
        var runtime = new FakeRuntime
        {
            CircuitState = "Closed",
            LastSuccessfulProviderCallAt = utcNow.AddMinutes(-10),
            LastFailureAt = utcNow.AddMinutes(-1),
            LastFailureCategory = "Unavailable"
        };
        var policy = new ConnectorHealthStatusPolicy(Options.Create(new ConnectorHealthOptions
        {
            OfflineNoSuccessSeconds = 300
        }));

        var status = policy.ResolveStatus(
            runtime,
            new IntegrationBacklogMetrics(0, 0, 0, 0, null),
            recentRateLimitCount: 0,
            utcNow);

        Assert.Equal(ConnectorHealthStatuses.Offline, status);
    }

    [Fact]
    public void Snapshot_includes_unresolved_dependency_and_freshness_inputs()
    {
        var utcNow = DateTimeOffset.Parse("2026-07-17T12:00:00Z");
        var oldest = utcNow.AddMinutes(-12);
        var runtime = new FakeRuntime
        {
            CircuitState = "Closed",
            LastSuccessfulProviderCallAt = utcNow.AddMinutes(-2)
        };
        var policy = new ConnectorHealthStatusPolicy(Options.Create(new ConnectorHealthOptions()));
        var snapshot = policy.Evaluate(
            runtime,
            new IntegrationBacklogMetrics(
                InboxBacklogCount: 3,
                OutboxBacklogCount: 1,
                DeadLetterCount: 0,
                UnresolvedDependencyCount: 2,
                OldestBacklogCreatedAt: oldest),
            lastSuccessfulSyncAt: utcNow.AddMinutes(-5),
            utcNow);

        Assert.Equal(2, snapshot.UnresolvedDependencyCount);
        Assert.Equal(3, snapshot.InboxBacklogCount);
        Assert.Equal(utcNow.AddMinutes(-5), snapshot.LastSuccessfulSyncAt);
        Assert.NotNull(snapshot.OldestBacklogAgeSeconds);
        Assert.InRange(snapshot.OldestBacklogAgeSeconds!.Value, 11 * 60, 13 * 60);
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
