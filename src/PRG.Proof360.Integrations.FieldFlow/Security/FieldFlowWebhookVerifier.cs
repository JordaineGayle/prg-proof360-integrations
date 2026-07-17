using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow.Security;

/// <summary>
/// Verifies FieldFlow webhooks: HMAC-SHA256 over <c>{unixSeconds}.{rawBody}</c>, timestamp skew, instance binding.
/// </summary>
public sealed class FieldFlowWebhookVerifier : IWebhookVerifier
{
    private readonly FieldFlowOptions _options;

    /// <summary>Creates the verifier.</summary>
    public FieldFlowWebhookVerifier(IOptions<FieldFlowOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public WebhookVerificationResult Verify(WebhookVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.WebhookHmacSecret))
        {
            return WebhookVerificationResult.Invalid("misconfigured", "Webhook HMAC secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Signature) ||
            string.IsNullOrWhiteSpace(request.TimestampHeader))
        {
            return WebhookVerificationResult.Invalid("missing_signature", "Signature and timestamp headers are required.");
        }

        if (!long.TryParse(request.TimestampHeader, out var unixSeconds))
        {
            return WebhookVerificationResult.Invalid("invalid_timestamp", "Timestamp header is not a unix seconds value.");
        }

        var skew = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - unixSeconds);
        if (skew > _options.WebhookTimestampSkewSeconds)
        {
            return WebhookVerificationResult.Invalid("timestamp_skew", "Webhook timestamp is outside the allowed skew window.");
        }

        if (!string.IsNullOrWhiteSpace(request.ProviderInstanceHeader) &&
            !string.Equals(request.ProviderInstanceHeader, _options.ProviderInstanceId, StringComparison.Ordinal))
        {
            return WebhookVerificationResult.Invalid("provider_instance_mismatch", "Provider instance header does not match configuration.");
        }

        var expected = Sign(_options.WebhookHmacSecret, unixSeconds, request.RawBody.Span);
        if (!FixedTimeEqualsHex(expected, request.Signature))
        {
            return WebhookVerificationResult.Invalid("invalid_signature", "Webhook signature verification failed.");
        }

        return WebhookVerificationResult.Valid();
    }

    /// <summary>Computes the canonical hex signature (test/demo helper).</summary>
    public static string Sign(string secret, long unixSeconds, ReadOnlySpan<byte> rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes($"{unixSeconds}.");
        var buffer = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(buffer);
        rawBody.CopyTo(buffer.AsSpan(prefix.Length));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(buffer)).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string expectedHex, string provided)
    {
        var normalized = provided.Trim().ToLowerInvariant();
        if (normalized.StartsWith("sha256=", StringComparison.Ordinal))
        {
            normalized = normalized["sha256=".Length..];
        }

        var a = Encoding.UTF8.GetBytes(expectedHex);
        var b = Encoding.UTF8.GetBytes(normalized);
        if (a.Length != b.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
