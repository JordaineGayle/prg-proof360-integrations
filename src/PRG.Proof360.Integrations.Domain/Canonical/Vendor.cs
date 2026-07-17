namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Proof360 canonical Vendor representation. Field names are locked by the assignment.
/// Nullability: compliance and insurance fields may be missing on import; AI fields remain null until a real AI process runs;
/// <see cref="CreatedAt"/> is Proof360-owned and immutable after insert.
/// </summary>
public sealed class Vendor
{
    /// <summary>Internal Proof360 identifier. Never a FieldFlow contractor ID.</summary>
    [CanonicalField("vendor_id")]
    public Guid VendorId { get; set; }

    /// <summary>Provider-supplied compliance identifier when available.</summary>
    [CanonicalField("compliance_id")]
    public string? ComplianceId { get; set; }

    /// <summary>License number when available.</summary>
    [CanonicalField("license_number")]
    public string? LicenseNumber { get; set; }

    /// <summary>License expiry as a calendar date (no time component).</summary>
    [CanonicalField("license_expiry")]
    public DateOnly? LicenseExpiry { get; set; }

    /// <summary>Insurance policy identifier when available.</summary>
    [CanonicalField("insurance_policy")]
    public string? InsurancePolicy { get; set; }

    /// <summary>Insurance expiry as a calendar date.</summary>
    [CanonicalField("insurance_expiry")]
    public DateOnly? InsuranceExpiry { get; set; }

    /// <summary>Insurance coverage description or limit text when available.</summary>
    [CanonicalField("insurance_coverage")]
    public string? InsuranceCoverage { get; set; }

    /// <summary>Workers compensation board number when available.</summary>
    [CanonicalField("wcb_number")]
    public string? WcbNumber { get; set; }

    /// <summary>Vendor lifecycle status (for example pending_review, approved, restricted).</summary>
    [CanonicalField("status")]
    public string Status { get; set; } = VendorStatuses.PendingReview;

    /// <summary>AI confidence when produced by an AI process; otherwise null.</summary>
    [CanonicalField("ai_confidence")]
    public decimal? AiConfidence { get; set; }

    /// <summary>Deterministic list of missing compliance items (serialized text), not raw provider JSON.</summary>
    [CanonicalField("missing_items")]
    public string? MissingItems { get; set; }

    /// <summary>Concise Proof360 policy rationale; never an arbitrary payload dump.</summary>
    [CanonicalField("rationale")]
    public string? Rationale { get; set; }

    /// <summary>UTC insertion timestamp owned by Proof360.</summary>
    [CanonicalField("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
