using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Api;
using PRG.Proof360.Integrations.Application.Health;
using PRG.Proof360.Integrations.FieldFlow.Resilience;

namespace PRG.Proof360.Integrations.ResilienceTests;

public sealed class HostHealthEndpointTests
{
    [Fact]
    public async Task Liveness_remains_healthy_during_provider_outage()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();
        var state = factory.Services.GetRequiredService<FieldFlowResilienceState>();
        state.SetCircuitOpen(DateTimeOffset.UtcNow);

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        using var connector = await client.GetAsync(new Uri("/connectors/fieldflow/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, connector.StatusCode);
        var snapshot = await connector.Content.ReadFromJsonAsync<ConnectorHealthSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(ConnectorHealthStatuses.Offline, snapshot.Status);
        Assert.Equal(FieldFlowCircuitStates.Open, snapshot.CircuitState);
    }

    [Fact]
    public async Task Connector_health_response_has_no_sensitive_markers()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/connectors/fieldflow/health", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        foreach (var marker in new[] { "replace-me", "password", "Bearer ", "@example.com", "+1-555", "WebhookHmacSecret" })
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
