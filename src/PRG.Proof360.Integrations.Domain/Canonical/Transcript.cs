namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Proof360 canonical Transcript representation. Represented for schema lock; not used by Phase 1 connector flows.
/// Preserves assignment capitalization for <c>Raw_text</c> and <c>City</c> at the persistence boundary.
/// </summary>
public sealed class Transcript
{
    /// <summary>Internal Proof360 transcript identifier.</summary>
    [CanonicalField("transcript_id")]
    public Guid TranscriptId { get; set; }

    /// <summary>Optional related Vendor identifier.</summary>
    [CanonicalField("vendor_ref")]
    public Guid? VendorRef { get; set; }

    /// <summary>Optional related Job identifier.</summary>
    [CanonicalField("job_ref")]
    public Guid? JobRef { get; set; }

    /// <summary>Call direction when known.</summary>
    [CanonicalField("direction")]
    public string? Direction { get; set; }

    /// <summary>Agent name when known.</summary>
    [CanonicalField("agent_name")]
    public string? AgentName { get; set; }

    /// <summary>Contact phone when known.</summary>
    [CanonicalField("contact_phone")]
    public string? ContactPhone { get; set; }

    /// <summary>Contact email when known.</summary>
    [CanonicalField("contact_email")]
    public string? ContactEmail { get; set; }

    /// <summary>Call start timestamp in UTC.</summary>
    [CanonicalField("call_start")]
    public DateTimeOffset? CallStart { get; set; }

    /// <summary>Call end timestamp in UTC.</summary>
    [CanonicalField("call_end")]
    public DateTimeOffset? CallEnd { get; set; }

    /// <summary>Call duration in seconds when known.</summary>
    [CanonicalField("duration")]
    public int? Duration { get; set; }

    /// <summary>Call summary when known.</summary>
    [CanonicalField("summary")]
    public string? Summary { get; set; }

    /// <summary>Topics when known.</summary>
    [CanonicalField("topics")]
    public string? Topics { get; set; }

    /// <summary>Sentiment when known.</summary>
    [CanonicalField("sentiment")]
    public string? Sentiment { get; set; }

    /// <summary>Last sync timestamp in UTC.</summary>
    [CanonicalField("synced_at")]
    public DateTimeOffset? SyncedAt { get; set; }

    /// <summary>Raw transcript text. External/database name is exactly <c>Raw_text</c>.</summary>
    [CanonicalField("Raw_text")]
    public string? RawText { get; set; }

    /// <summary>City associated with the call. External/database name is exactly <c>City</c>.</summary>
    [CanonicalField("City")]
    public string? City { get; set; }

    /// <summary>Transcript status when known.</summary>
    [CanonicalField("status")]
    public string? Status { get; set; }
}
