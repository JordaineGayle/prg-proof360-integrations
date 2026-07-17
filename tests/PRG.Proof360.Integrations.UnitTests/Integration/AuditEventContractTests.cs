using System.Reflection;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.UnitTests.Integration;

public sealed class AuditEventContractTests
{
    [Fact]
    public void Audit_event_contract_excludes_secret_and_raw_authorization_fields()
    {
        var names = typeof(AuditEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] forbidden =
        [
            "Authorization",
            "ApiKey",
            "AccessToken",
            "Secret",
            "Signature",
            "RawBody",
            "Payload",
            "WebhookBody",
            "Password",
            "Bearer"
        ];

        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, names);
        }

        Assert.Contains("PayloadHash", names);
        Assert.Contains("CorrelationId", names);
        Assert.Contains("ErrorCategory", names);
    }
}
