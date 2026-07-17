namespace PRG.Proof360.Integrations.Application.Dispatch;

/// <summary>
/// Options for outbound outbox processing.
/// </summary>
public sealed class OutboundDispatchOptions
{
    /// <summary>Section name.</summary>
    public const string SectionName = "OutboundDispatch";

    /// <summary>Enable background outbox worker.</summary>
    public bool WorkerEnabled { get; set; }

    /// <summary>Worker poll interval in seconds.</summary>
    public int WorkerIntervalSeconds { get; set; } = 5;

    /// <summary>Max outbox messages to process per cycle.</summary>
    public int MaxProcessBatch { get; set; } = 50;

    /// <summary>Max outbox attempts before dead-letter (Prompt 08 owns HTTP retries).</summary>
    public int MaxAttempts { get; set; } = 8;
}
