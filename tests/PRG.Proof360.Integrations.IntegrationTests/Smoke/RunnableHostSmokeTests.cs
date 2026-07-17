using Microsoft.AspNetCore.Mvc.Testing;
using PRG.Proof360.Integrations.Api;

namespace PRG.Proof360.Integrations.IntegrationTests.Smoke;

public sealed class RunnableHostSmokeTests
{
    [Fact]
    public async Task Connector_api_live_health_returns_success()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Connector_api_ready_health_returns_success()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.True(response.IsSuccessStatusCode);
    }
}
