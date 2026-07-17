using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.IntegrationTests.Persistence;

public sealed class CanonicalColumnTests
{
    [Fact]
    public async Task Ef_model_uses_exact_assignment_column_names()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var model = database.DbContext.Model;

        AssertColumns(model.FindEntityType(typeof(Vendor))!,
            "vendor_id", "compliance_id", "license_number", "license_expiry", "insurance_policy",
            "insurance_expiry", "insurance_coverage", "wcb_number", "status", "ai_confidence",
            "missing_items", "rationale", "created_at");

        AssertColumns(model.FindEntityType(typeof(Job))!,
            "job_id", "source", "transcript_id", "customer_name", "customer_phone", "customer_email",
            "address_street", "address_unit", "address_city", "address_postal", "service_type",
            "subcategory", "priority", "window_start", "window_end", "notes_scope", "compliance_only",
            "status", "assigned_vendor_id", "ai_confidence", "ai_json");

        AssertColumns(model.FindEntityType(typeof(Transcript))!,
            "transcript_id", "vendor_ref", "job_ref", "direction", "agent_name", "contact_phone",
            "contact_email", "call_start", "call_end", "duration", "summary", "topics", "sentiment",
            "synced_at", "Raw_text", "City", "status");
    }

    private static void AssertColumns(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, params string[] expected)
    {
        var actual = entityType.GetProperties()
            .Select(property => property.GetColumnName())
            .Where(name => name is not null && !string.Equals(name, "Id", StringComparison.Ordinal))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(), actual);
    }
}
