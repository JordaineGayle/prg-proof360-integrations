using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using PRG.FieldFlow.Mock;

namespace PRG.Proof360.Integrations.IntegrationTests.Smoke;

public sealed class MockHostSmokeTests
{
    [Fact]
    public async Task FieldFlow_mock_health_returns_healthy()
    {
        await using var factory = new WebApplicationFactory<MockAssemblyMarker>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        var payload = await response.Content.ReadFromJsonAsync<MockHealthResponse>();

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
        Assert.Equal("PRG.FieldFlow.Mock", payload.Service);
    }

    private sealed record MockHealthResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("service")] string Service);
}
