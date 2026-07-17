namespace PRG.Proof360.Integrations.Application.Observability;

/// <summary>Stable audit operation names (low cardinality).</summary>
public static class AuditOperations
{
    /// <summary>Manual/worker sync requested.</summary>
    public const string SyncRequested = "sync.requested";

    /// <summary>Sync cycle completed.</summary>
    public const string SyncCompleted = "sync.completed";

    /// <summary>Sync cycle failed before durable progress.</summary>
    public const string SyncFailed = "sync.failed";

    /// <summary>Webhook signature/timestamp verification.</summary>
    public const string WebhookVerify = "webhook.verify";

    /// <summary>Webhook accepted into inbox.</summary>
    public const string WebhookAccepted = "webhook.accepted";

    /// <summary>Duplicate webhook receipt.</summary>
    public const string WebhookDuplicate = "webhook.duplicate";

    /// <summary>Contractor apply path.</summary>
    public const string ContractorApply = "contractor.apply";

    /// <summary>Work-order apply path.</summary>
    public const string WorkOrderApply = "work_order.apply";

    /// <summary>Dispatch queued.</summary>
    public const string DispatchRequested = "dispatch.requested";

    /// <summary>Dispatch completed / already dispatched.</summary>
    public const string DispatchCompleted = "dispatch.completed";

    /// <summary>Dispatch ambiguous / reconcile path.</summary>
    public const string DispatchAmbiguous = "dispatch.ambiguous";

    /// <summary>Inbox waiting for dependency.</summary>
    public const string DependencyWaiting = "dependency.waiting";

    /// <summary>Dependency resolved (message completed after wait).</summary>
    public const string DependencyResolved = "dependency.resolved";

    /// <summary>Inbox or outbox dead-lettered.</summary>
    public const string DeadLettered = "message.dead_lettered";

    /// <summary>Operator replay requested.</summary>
    public const string ReplayRequested = "replay.requested";

    /// <summary>Replay completed (message re-queued).</summary>
    public const string ReplayCompleted = "replay.completed";

    /// <summary>Circuit state transition.</summary>
    public const string CircuitTransition = "circuit.transition";
}
