namespace PRG.Proof360.Integrations.Application.Outcomes;

/// <summary>
/// Typed success outcomes for inbound webhook receipt. Duplicate is success (idempotent).
/// </summary>
public abstract record ReceiveEventOutcome
{
    private ReceiveEventOutcome()
    {
    }

    /// <summary>Event accepted into the inbox.</summary>
    /// <param name="InboxMessageId">Inbox row id.</param>
    public sealed record Accepted(Guid InboxMessageId) : ReceiveEventOutcome;

    /// <summary>Duplicate event already received.</summary>
    /// <param name="ExistingInboxMessageId">Existing inbox row id.</param>
    public sealed record Duplicate(Guid ExistingInboxMessageId) : ReceiveEventOutcome;
}

/// <summary>
/// Typed success outcomes for applying a work-order event. Stale/no-change are success.
/// </summary>
public abstract record ApplyWorkOrderOutcome
{
    private ApplyWorkOrderOutcome()
    {
    }

    /// <summary>Job created.</summary>
    public sealed record Created(Guid JobId) : ApplyWorkOrderOutcome;

    /// <summary>Job updated.</summary>
    public sealed record Updated(Guid JobId) : ApplyWorkOrderOutcome;

    /// <summary>No mutation required.</summary>
    public sealed record NoChange(Guid JobId) : ApplyWorkOrderOutcome;

    /// <summary>Stale version ignored (audited no-op).</summary>
    public sealed record IgnoredStale(Guid JobId, long AppliedVersion) : ApplyWorkOrderOutcome;

    /// <summary>Invalid transition ignored in async processing (audited).</summary>
    public sealed record IgnoredInvalidTransition(Guid JobId, string ReasonCode) : ApplyWorkOrderOutcome;

    /// <summary>Equal version with different payload hash — security anomaly, no mutation.</summary>
    public sealed record VersionPayloadConflict(Guid JobId, long Version) : ApplyWorkOrderOutcome;
}

/// <summary>
/// Typed success outcomes for qualified Job dispatch.
/// </summary>
public abstract record DispatchJobOutcome
{
    private DispatchJobOutcome()
    {
    }

    /// <summary>Dispatch accepted / work order created.</summary>
    public sealed record Dispatched(Guid JobId, string ExternalWorkOrderId) : DispatchJobOutcome;

    /// <summary>Idempotent replay of a prior dispatch.</summary>
    public sealed record AlreadyDispatched(Guid JobId, string ExternalWorkOrderId) : DispatchJobOutcome;
}

/// <summary>
/// Typed success outcomes for dead-letter replay.
/// </summary>
public abstract record ReplayOutcome
{
    private ReplayOutcome()
    {
    }

    /// <summary>Replay accepted.</summary>
    public sealed record Accepted(Guid InboxMessageId) : ReplayOutcome;

    /// <summary>Replay not needed / already complete.</summary>
    public sealed record AlreadyComplete(Guid InboxMessageId) : ReplayOutcome;
}

/// <summary>
/// Typed success outcomes for contractor import.
/// </summary>
public abstract record ImportContractorsOutcome
{
    private ImportContractorsOutcome()
    {
    }

    /// <summary>Import pass completed.</summary>
    /// <param name="ImportedCount">Vendors created.</param>
    /// <param name="UpdatedCount">Vendors updated.</param>
    public sealed record Completed(int ImportedCount, int UpdatedCount) : ImportContractorsOutcome;
}
