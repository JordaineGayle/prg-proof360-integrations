using System.Reflection;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.UnitTests.Canonical;

public sealed class CanonicalFieldContractTests
{
    [Theory]
    [InlineData(typeof(Vendor), "vendor_id", "compliance_id", "license_number", "license_expiry", "insurance_policy", "insurance_expiry", "insurance_coverage", "wcb_number", "status", "ai_confidence", "missing_items", "rationale", "created_at")]
    [InlineData(typeof(Job), "job_id", "source", "transcript_id", "customer_name", "customer_phone", "customer_email", "address_street", "address_unit", "address_city", "address_postal", "service_type", "subcategory", "priority", "window_start", "window_end", "notes_scope", "compliance_only", "status", "assigned_vendor_id", "ai_confidence", "ai_json")]
    [InlineData(typeof(Transcript), "transcript_id", "vendor_ref", "job_ref", "direction", "agent_name", "contact_phone", "contact_email", "call_start", "call_end", "duration", "summary", "topics", "sentiment", "synced_at", "Raw_text", "City", "status")]
    public void Canonical_type_exposes_exact_assignment_fields(Type type, params string[] expectedFields)
    {
        var actual = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetCustomAttribute<CanonicalFieldAttribute>()?.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = expectedFields.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }
}
