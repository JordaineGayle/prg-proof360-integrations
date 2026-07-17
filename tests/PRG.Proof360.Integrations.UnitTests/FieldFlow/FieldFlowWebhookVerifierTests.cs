using System.Text;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.FieldFlow;
using PRG.Proof360.Integrations.FieldFlow.Security;

namespace PRG.Proof360.Integrations.UnitTests.FieldFlow;

public sealed class FieldFlowWebhookVerifierTests
{
    private const string Secret = "unit-test-hmac-secret";
    private const string Instance = "fieldflow-test-1";

    [Fact]
    public void Valid_signature_over_raw_bytes_is_accepted()
    {
        var body = Encoding.UTF8.GetBytes("""{"eventId":"e1"}""");
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = FieldFlowWebhookVerifier.Sign(Secret, unix, body);
        var verifier = CreateVerifier();

        var result = verifier.Verify(new WebhookVerificationRequest
        {
            RawBody = body,
            Signature = signature,
            TimestampHeader = unix.ToString(),
            ProviderInstanceHeader = Instance,
            EventIdHeader = "e1"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Json_reformatting_changes_signature_and_fails()
    {
        var compact = Encoding.UTF8.GetBytes("{\"eventId\":\"e1\"}");
        var pretty = Encoding.UTF8.GetBytes("{\n  \"eventId\": \"e1\"\n}");
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = FieldFlowWebhookVerifier.Sign(Secret, unix, compact);
        var verifier = CreateVerifier();

        var result = verifier.Verify(new WebhookVerificationRequest
        {
            RawBody = pretty,
            Signature = signature,
            TimestampHeader = unix.ToString(),
            ProviderInstanceHeader = Instance
        });

        Assert.False(result.IsValid);
        Assert.Equal("invalid_signature", result.FailureCode);
    }

    [Fact]
    public void Stale_timestamp_is_rejected()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10_000;
        var signature = FieldFlowWebhookVerifier.Sign(Secret, unix, body);
        var verifier = CreateVerifier(skewSeconds: 300);

        var result = verifier.Verify(new WebhookVerificationRequest
        {
            RawBody = body,
            Signature = signature,
            TimestampHeader = unix.ToString(),
            ProviderInstanceHeader = Instance
        });

        Assert.False(result.IsValid);
        Assert.Equal("timestamp_skew", result.FailureCode);
    }

    private static FieldFlowWebhookVerifier CreateVerifier(int skewSeconds = 300) =>
        new(Options.Create(new FieldFlowOptions
        {
            BaseUrl = "http://localhost:5210",
            WebhookHmacSecret = Secret,
            ProviderInstanceId = Instance,
            WebhookTimestampSkewSeconds = skewSeconds
        }));
}
