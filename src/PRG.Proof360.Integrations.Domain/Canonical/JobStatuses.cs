namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Assumed Proof360 Job status vocabulary. PRG did not supply an official enumeration; see assumptions.md.
/// </summary>
public static class JobStatuses
{
    /// <summary>Job is ready for outbound provider dispatch.</summary>
    public const string Qualified = "qualified";

    /// <summary>Job has been sent to the field-service provider.</summary>
    public const string Dispatched = "dispatched";

    /// <summary>Provider has scheduled the work.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>Work is actively in progress.</summary>
    public const string InProgress = "in_progress";

    /// <summary>Terminal successful completion.</summary>
    public const string Completed = "completed";

    /// <summary>Terminal cancellation.</summary>
    public const string Cancelled = "cancelled";
}
