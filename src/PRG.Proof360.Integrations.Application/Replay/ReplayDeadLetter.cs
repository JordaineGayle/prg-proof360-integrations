using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Observability;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Observability;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Replay;

/// <summary>Command to replay a dead-lettered inbox message.</summary>
public sealed class ReplayDeadLetterCommand
{
    /// <summary>Inbox message id.</summary>
    public required Guid InboxMessageId { get; init; }

    /// <summary>Operator identity (required).</summary>
    public required string OperatorId { get; init; }

    /// <summary>Human reason for replay (required).</summary>
    public required string Reason { get; init; }

    /// <summary>Caller correlation id.</summary>
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Re-queues a DeadLettered inbox message. Retains event identity and failure history;
/// creates a new causation id for the replay attempt.
/// </summary>
public sealed class ReplayDeadLetterHandler
{
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly StructuredAuditWriter _audit;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ReplayDeadLetterHandler(
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        StructuredAuditWriter audit,
        IClock clock)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>Replays one dead-lettered inbox message.</summary>
    public async Task<Result<ReplayOutcome, IntegrationFailure>> HandleAsync(
        ReplayDeadLetterCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.OperatorId) || command.OperatorId.Length > 128)
        {
            return Result<ReplayOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Validation(FailureCodes.RequiredFieldMissing, "OperatorId is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500)
        {
            return Result<ReplayOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Validation(FailureCodes.RequiredFieldMissing, "Reason is required (max 500 chars)."));
        }

        using var activity = ConnectorTelemetry.StartActivity("replay.dead_letter", AuditOperations.ReplayRequested);
        var causationId = CorrelationIdRules.NewId();
        var correlationId = CorrelationIdRules.Resolve(command.CorrelationId);

        var message = await _store.FindInboxByIdAsync(command.InboxMessageId, cancellationToken);
        if (message is null)
        {
            return Result<ReplayOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.ProviderNotFound,
                    "Inbox message was not found.",
                    FailureCategory.NotFound));
        }

        if (message.State == InboxMessageStates.Completed)
        {
            await WriteReplayAuditAsync(
                message,
                correlationId,
                causationId,
                command,
                "already_complete",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ReplayOutcome, IntegrationFailure>.Ok(new ReplayOutcome.AlreadyComplete(message.Id));
        }

        if (message.State != InboxMessageStates.DeadLettered)
        {
            return Result<ReplayOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.ValidationFailed,
                    "Only DeadLettered inbox messages can be replayed.",
                    FailureCategory.Validation));
        }

        var historyBefore = message.FailureHistoryJson;

        await _audit.WriteAsync(
            new AuditWriteRequest
            {
                Operation = AuditOperations.ReplayRequested,
                Result = "accepted",
                Direction = AuditDirections.Internal,
                CorrelationId = correlationId,
                CausationId = causationId,
                ProviderName = message.ProviderName,
                ProviderInstanceId = message.ProviderInstanceId,
                EventId = message.EventId,
                Attempt = message.AttemptCount,
                PayloadHash = message.PayloadHash,
                SchemaVersion = message.SchemaVersion,
                ErrorCategory = Truncate($"operator={command.OperatorId.Trim()}; reason={command.Reason.Trim()}", 200)
            },
            cancellationToken);

        // Re-queue: keep EventId, PayloadHash, FailureHistoryJson. Clear last-error fields only.
        message.State = InboxMessageStates.Pending;
        message.NextAttemptAt = null;
        message.ErrorCategory = null;
        message.ErrorMessage = null;
        message.CausationId = causationId;
        message.CorrelationId = correlationId;
        message.RowVersion += 1;

        await _audit.WriteAsync(
            new AuditWriteRequest
            {
                Operation = AuditOperations.ReplayCompleted,
                Result = "requeued",
                Direction = AuditDirections.Internal,
                CorrelationId = correlationId,
                CausationId = causationId,
                ProviderName = message.ProviderName,
                ProviderInstanceId = message.ProviderInstanceId,
                EventId = message.EventId,
                Attempt = message.AttemptCount,
                PayloadHash = message.PayloadHash,
                SchemaVersion = message.SchemaVersion
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Guarantee failure history was not erased.
        if (!string.Equals(historyBefore, message.FailureHistoryJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Replay must not mutate failure history.");
        }

        ConnectorTelemetry.RecordInbox("retried");
        return Result<ReplayOutcome, IntegrationFailure>.Ok(new ReplayOutcome.Accepted(message.Id));
    }

    private Task WriteReplayAuditAsync(
        InboxMessage message,
        string correlationId,
        string causationId,
        ReplayDeadLetterCommand command,
        string result,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            new AuditWriteRequest
            {
                Operation = AuditOperations.ReplayRequested,
                Result = result,
                Direction = AuditDirections.Internal,
                CorrelationId = correlationId,
                CausationId = causationId,
                ProviderName = message.ProviderName,
                ProviderInstanceId = message.ProviderInstanceId,
                EventId = message.EventId,
                Attempt = message.AttemptCount,
                PayloadHash = message.PayloadHash,
                ErrorCategory = Truncate($"operator={command.OperatorId.Trim()}", 200)
            },
            cancellationToken);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
