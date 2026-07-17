using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PRG.Proof360.Integrations.Core.Observability;

/// <summary>
/// Low-cardinality BCL metrics and activities for the connector.
/// Labels must never include customer IDs, external IDs, correlation IDs, or raw errors.
/// </summary>
public static class ConnectorTelemetry
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "PRG.Proof360.Integrations";

    /// <summary>Activity source name.</summary>
    public const string ActivitySourceName = "PRG.Proof360.Integrations";

    private static readonly Meter Meter = new(MeterName);
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Provider HTTP requests by operation/outcome.</summary>
    public static readonly Counter<long> ProviderRequests =
        Meter.CreateCounter<long>("connector.provider.requests", unit: "{request}");

    /// <summary>Provider HTTP duration milliseconds by operation/outcome.</summary>
    public static readonly Histogram<double> ProviderDurationMs =
        Meter.CreateHistogram<double>("connector.provider.duration", unit: "ms");

    /// <summary>HTTP retry attempts (not business outbox attempts).</summary>
    public static readonly Counter<long> HttpRetries =
        Meter.CreateCounter<long>("connector.http.retries", unit: "{retry}");

    /// <summary>Rate-limit outcomes.</summary>
    public static readonly Counter<long> RateLimits =
        Meter.CreateCounter<long>("connector.http.rate_limits", unit: "{event}");

    /// <summary>Circuit transitions (opened|half_open|closed).</summary>
    public static readonly Counter<long> CircuitTransitions =
        Meter.CreateCounter<long>("connector.circuit.transitions", unit: "{transition}");

    /// <summary>Webhook outcomes (accepted|rejected|duplicate|stale).</summary>
    public static readonly Counter<long> Webhooks =
        Meter.CreateCounter<long>("connector.webhooks", unit: "{event}");

    /// <summary>Inbox processing outcomes.</summary>
    public static readonly Counter<long> InboxProcessed =
        Meter.CreateCounter<long>("connector.inbox.processed", unit: "{message}");

    /// <summary>Outbox processing outcomes.</summary>
    public static readonly Counter<long> OutboxProcessed =
        Meter.CreateCounter<long>("connector.outbox.processed", unit: "{message}");

    /// <summary>Dead-letter transitions.</summary>
    public static readonly Counter<long> DeadLettered =
        Meter.CreateCounter<long>("connector.dead_lettered", unit: "{message}");

    /// <summary>Unresolved dependency waits.</summary>
    public static readonly Counter<long> UnresolvedDependencies =
        Meter.CreateCounter<long>("connector.dependencies.unresolved", unit: "{message}");

    /// <summary>Sync cycles by entity/outcome.</summary>
    public static readonly Counter<long> SyncCycles =
        Meter.CreateCounter<long>("connector.sync.cycles", unit: "{cycle}");

    /// <summary>Starts a use-case activity with safe tags only.</summary>
    public static Activity? StartActivity(string name, string? operation = null)
    {
        var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        if (activity is not null && operation is not null)
        {
            activity.SetTag("connector.operation", operation);
        }

        return activity;
    }

    /// <summary>Records a provider request with low-cardinality labels.</summary>
    public static void RecordProviderRequest(string operation, string outcome, double durationMs)
    {
        var tags = new TagList
        {
            { "operation", SanitizeLabel(operation) },
            { "outcome", SanitizeLabel(outcome) }
        };
        ProviderRequests.Add(1, tags);
        ProviderDurationMs.Record(durationMs, tags);
    }

    /// <summary>Records a circuit transition.</summary>
    public static void RecordCircuitTransition(string state) =>
        CircuitTransitions.Add(1, new TagList { { "state", SanitizeLabel(state) } });

    /// <summary>Records a webhook outcome.</summary>
    public static void RecordWebhook(string outcome) =>
        Webhooks.Add(1, new TagList { { "outcome", SanitizeLabel(outcome) } });

    /// <summary>Records inbox processing.</summary>
    public static void RecordInbox(string outcome) =>
        InboxProcessed.Add(1, new TagList { { "outcome", SanitizeLabel(outcome) } });

    /// <summary>Records outbox processing.</summary>
    public static void RecordOutbox(string outcome) =>
        OutboxProcessed.Add(1, new TagList { { "outcome", SanitizeLabel(outcome) } });

    /// <summary>Records dead-letter (channel = inbox|outbox).</summary>
    public static void RecordDeadLetter(string channel) =>
        DeadLettered.Add(1, new TagList { { "channel", SanitizeLabel(channel) } });

    /// <summary>Records sync cycle.</summary>
    public static void RecordSync(string entity, string outcome) =>
        SyncCycles.Add(
            1,
            new TagList
            {
                { "entity", SanitizeLabel(entity) },
                { "outcome", SanitizeLabel(outcome) }
            });

    /// <summary>
    /// Allowed label characters only; rejects high-cardinality-looking values by length.
    /// </summary>
    public static string SanitizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.Length > 32)
        {
            return "invalid";
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not '_' and not '-' and not '.')
            {
                return "invalid";
            }
        }

        return trimmed;
    }

    /// <summary>Known safe outcome labels for tests.</summary>
    public static readonly HashSet<string> AllowedOutcomeLabels =
    [
        "success",
        "failure",
        "timeout",
        "rate_limited",
        "circuit_open",
        "validation",
        "auth",
        "accepted",
        "rejected",
        "duplicate",
        "stale",
        "completed",
        "dead_lettered",
        "waiting",
        "retried",
        "ambiguous",
        "reconciled",
        "opened",
        "half_open",
        "closed",
        "requested",
        "idle"
    ];
}
