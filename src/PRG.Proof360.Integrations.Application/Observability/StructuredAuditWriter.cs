using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;

namespace PRG.Proof360.Integrations.Application.Observability;

/// <summary>
/// Stages append-only sanitized audit events. Caller owns SaveChanges.
/// </summary>
public sealed class StructuredAuditWriter
{
    private readonly IIntegrationStore _store;
    private readonly IClock _clock;

    /// <summary>Creates the writer.</summary>
    public StructuredAuditWriter(IIntegrationStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>Stages one audit event.</summary>
    public Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = request.CorrelationId,
                CausationId = request.CausationId,
                Direction = request.Direction,
                ProviderName = request.ProviderName ?? ProviderNames.FieldFlow,
                ProviderInstanceId = request.ProviderInstanceId,
                Operation = request.Operation,
                CanonicalEntityType = request.CanonicalEntityType,
                CanonicalId = request.CanonicalId,
                EventId = request.EventId,
                Attempt = request.Attempt,
                Result = request.Result,
                ErrorCategory = request.ErrorCategory,
                LatencyMilliseconds = request.LatencyMilliseconds,
                SchemaVersion = request.SchemaVersion,
                PayloadHash = request.PayloadHash,
                Timestamp = _clock.UtcNow
            },
            cancellationToken);
    }
}

/// <summary>Sanitized audit write request.</summary>
public sealed class AuditWriteRequest
{
    /// <summary>Gets or sets Operation.</summary>
    public required string Operation { get; init; }

    /// <summary>Gets or sets Result.</summary>
    public required string Result { get; init; }

    /// <summary>Gets or sets Direction.</summary>
    public string Direction { get; init; } = AuditDirections.Internal;

    /// <summary>Gets or sets CorrelationId.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets or sets CausationId.</summary>
    public string? CausationId { get; init; }

    /// <summary>Gets or sets ProviderName.</summary>
    public string? ProviderName { get; init; }

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string? ProviderInstanceId { get; init; }

    /// <summary>Gets or sets CanonicalEntityType.</summary>
    public string? CanonicalEntityType { get; init; }

    /// <summary>Gets or sets CanonicalId.</summary>
    public Guid? CanonicalId { get; init; }

    /// <summary>Gets or sets EventId.</summary>
    public string? EventId { get; init; }

    /// <summary>Gets or sets Attempt.</summary>
    public int Attempt { get; init; }

    /// <summary>Gets or sets ErrorCategory.</summary>
    public string? ErrorCategory { get; init; }

    /// <summary>Gets or sets LatencyMilliseconds.</summary>
    public long? LatencyMilliseconds { get; init; }

    /// <summary>Gets or sets SchemaVersion.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Gets or sets PayloadHash.</summary>
    public string? PayloadHash { get; init; }
}
