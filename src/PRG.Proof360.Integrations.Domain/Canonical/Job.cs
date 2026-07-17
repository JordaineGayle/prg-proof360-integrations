namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Proof360 canonical Job representation. Field names are locked by the assignment.
/// Nullability: customer/address/service fields may be null until a validated origin-specific import;
/// AI fields remain null unless produced by AI; <see cref="AiJson"/> is never used for lineage.
/// </summary>
public sealed class Job
{
    /// <summary>Internal Proof360 identifier. Never a FieldFlow work-order ID.</summary>
    [CanonicalField("job_id")]
    public Guid JobId { get; set; }

    /// <summary>Origin marker such as Proof360 or FieldFlow.</summary>
    [CanonicalField("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Optional related transcript identifier.</summary>
    [CanonicalField("transcript_id")]
    public Guid? TranscriptId { get; set; }

    /// <summary>Customer display name when known.</summary>
    [CanonicalField("customer_name")]
    public string? CustomerName { get; set; }

    /// <summary>Customer phone when known.</summary>
    [CanonicalField("customer_phone")]
    public string? CustomerPhone { get; set; }

    /// <summary>Customer email when known.</summary>
    [CanonicalField("customer_email")]
    public string? CustomerEmail { get; set; }

    /// <summary>Street address when known.</summary>
    [CanonicalField("address_street")]
    public string? AddressStreet { get; set; }

    /// <summary>Unit/suite when known.</summary>
    [CanonicalField("address_unit")]
    public string? AddressUnit { get; set; }

    /// <summary>City when known.</summary>
    [CanonicalField("address_city")]
    public string? AddressCity { get; set; }

    /// <summary>Postal code when known.</summary>
    [CanonicalField("address_postal")]
    public string? AddressPostal { get; set; }

    /// <summary>Primary service type when known.</summary>
    [CanonicalField("service_type")]
    public string? ServiceType { get; set; }

    /// <summary>Service subcategory when known.</summary>
    [CanonicalField("subcategory")]
    public string? Subcategory { get; set; }

    /// <summary>Job priority when known.</summary>
    [CanonicalField("priority")]
    public string? Priority { get; set; }

    /// <summary>Desired service window start in UTC.</summary>
    [CanonicalField("window_start")]
    public DateTimeOffset? WindowStart { get; set; }

    /// <summary>Desired service window end in UTC.</summary>
    [CanonicalField("window_end")]
    public DateTimeOffset? WindowEnd { get; set; }

    /// <summary>Scope notes; not a provider payload dump.</summary>
    [CanonicalField("notes_scope")]
    public string? NotesScope { get; set; }

    /// <summary>Whether the job is compliance-only.</summary>
    [CanonicalField("compliance_only")]
    public bool ComplianceOnly { get; set; }

    /// <summary>Operational status. Vocabulary is an assignment assumption documented in assumptions.md.</summary>
    [CanonicalField("status")]
    public string Status { get; set; } = JobStatuses.Qualified;

    /// <summary>Assigned Vendor when resolved through an identity link.</summary>
    [CanonicalField("assigned_vendor_id")]
    public Guid? AssignedVendorId { get; set; }

    /// <summary>AI confidence when produced by an AI process; otherwise null.</summary>
    [CanonicalField("ai_confidence")]
    public decimal? AiConfidence { get; set; }

    /// <summary>AI-produced JSON only; never lineage or arbitrary provider storage.</summary>
    [CanonicalField("ai_json")]
    public string? AiJson { get; set; }
}
