using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Inbox;

/// <summary>
/// Receipt command for a provider event or polling snapshot envelope.
/// </summary>
public sealed class ReceiveProviderEventCommand
{
    /// <summary>Provider name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Provider instance id.</summary>
    public required string ProviderInstanceId { get; init; }

    /// <summary>Deterministic event id.</summary>
    public required string EventId { get; init; }

    /// <summary>Event type.</summary>
    public required string EventType { get; init; }

    /// <summary>JSON payload envelope (snapshot), stored for replay — not canonical storage.</summary>
    public required string PayloadEnvelope { get; init; }

    /// <summary>Payload hash.</summary>
    public required string PayloadHash { get; init; }

    /// <summary>Provider entity/event version when known.</summary>
    public long? EventVersion { get; init; }

    /// <summary>Schema version.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Occurred-at UTC.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Correlation id.</summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Durable inbox receipt. Commits before processing. Unique (instance, eventId) → Duplicate.
/// </summary>
public sealed class ReceiveProviderEventHandler
{
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IPersistenceExceptionClassifier _exceptions;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ReceiveProviderEventHandler(
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        IPersistenceExceptionClassifier exceptions,
        IClock clock)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _exceptions = exceptions;
        _clock = clock;
    }

    /// <summary>
    /// Receipt transaction: validate identity, insert Pending inbox, commit.
    /// </summary>
    public async Task<Result<ReceiveEventOutcome, IntegrationFailure>> HandleAsync(
        ReceiveProviderEventCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.ProviderInstanceId) ||
            string.IsNullOrWhiteSpace(command.EventId) ||
            string.IsNullOrWhiteSpace(command.EventType) ||
            string.IsNullOrWhiteSpace(command.PayloadEnvelope))
        {
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Validation(
                    FailureCodes.RequiredFieldMissing,
                    "Provider instance, event id, event type, and payload are required."));
        }

        var existing = await _store.FindInboxByEventIdAsync(
            command.ProviderInstanceId,
            command.EventId,
            cancellationToken);
        if (existing is not null)
        {
            return Result<ReceiveEventOutcome, IntegrationFailure>.Ok(
                new ReceiveEventOutcome.Duplicate(existing.Id));
        }

        var message = new InboxMessage
        {
            Id = Guid.NewGuid(),
            ProviderName = command.ProviderName.Trim(),
            ProviderInstanceId = command.ProviderInstanceId.Trim(),
            EventId = command.EventId.Trim(),
            EventType = command.EventType.Trim(),
            SchemaVersion = command.SchemaVersion,
            EventVersion = command.EventVersion,
            CorrelationId = command.CorrelationId,
            OccurredAt = command.OccurredAt.ToUniversalTime(),
            ReceivedAt = _clock.UtcNow,
            PayloadEnvelope = command.PayloadEnvelope,
            PayloadHash = command.PayloadHash,
            State = InboxMessageStates.Pending,
            AttemptCount = 0,
            NextAttemptAt = null
        };

        await _store.AddInboxMessageAsync(message, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReceiveEventOutcome, IntegrationFailure>.Ok(
                new ReceiveEventOutcome.Accepted(message.Id));
        }
        catch (Exception ex) when (_exceptions.IsUniqueConstraintViolation(ex))
        {
            var duplicate = await _store.FindInboxByEventIdAsync(
                command.ProviderInstanceId,
                command.EventId,
                cancellationToken);
            if (duplicate is null)
            {
                return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                    new IntegrationFailure(
                        FailureCodes.ConcurrencyConflict,
                        "Inbox uniqueness conflict could not be resolved.",
                        FailureCategory.PersistenceConflict));
            }

            return Result<ReceiveEventOutcome, IntegrationFailure>.Ok(
                new ReceiveEventOutcome.Duplicate(duplicate.Id));
        }
    }
}
