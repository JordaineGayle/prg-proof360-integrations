using System.Security.Cryptography;
using System.Text;

namespace PRG.FieldFlow.Mock.Security;

/// <summary>
/// HMAC-SHA256 signer for mock webhook payloads.
/// Canonical signing string: <c>{unixSeconds}.{rawBody}</c>.
/// </summary>
public static class WebhookSigner
{
    /// <summary>
    /// Computes a lowercase hex HMAC for the raw body and unix timestamp.
    /// </summary>
    public static string Sign(string secret, long unixSeconds, ReadOnlySpan<byte> rawBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var prefix = Encoding.UTF8.GetBytes($"{unixSeconds}.");
        var buffer = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(buffer);
        rawBody.CopyTo(buffer.AsSpan(prefix.Length));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(buffer);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
