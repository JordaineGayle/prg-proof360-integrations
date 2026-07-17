using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PRG.FieldFlow.Mock;

namespace PRG.Proof360.Integrations.IntegrationTests.FieldFlowMock;

/// <summary>
/// WebApplicationFactory for the FieldFlow mock with deterministic test credentials.
/// </summary>
public sealed class FieldFlowMockFactory : WebApplicationFactory<MockAssemblyMarker>
{
    /// <summary>API key used by mock HTTP tests.</summary>
    public const string ApiKey = "test-mock-api-key";

    /// <summary>HMAC secret used by mock HTTP tests.</summary>
    public const string WebhookSecret = "test-mock-webhook-secret";

    /// <summary>Provider instance id used by mock HTTP tests.</summary>
    public const string ProviderInstanceId = "fieldflow-test-1";

    /// <inheritdoc />
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FieldFlowMock:ApiKey"] = ApiKey,
                ["FieldFlowMock:WebhookHmacSecret"] = WebhookSecret,
                ["FieldFlowMock:ProviderInstanceId"] = ProviderInstanceId,
                ["FieldFlowMock:MaxRequestBodyBytes"] = "65536"
            });
        });
    }

    /// <summary>Creates an HTTP client that sends the mock API key.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }
}
