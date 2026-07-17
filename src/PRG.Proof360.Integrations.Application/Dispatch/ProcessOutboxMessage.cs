using System.Text.Json;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Observability;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Observability;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Dispatch;

/// <summary>
/// Outcome of processing one outbox message.
/// </summary>
public abstract record ProcessOutboxOutcome
{
    private ProcessOutboxOutcome()
    {
    }

    /// <summary>No eligible outbox work.</summary>
    public sealed record Idle : ProcessOutboxOutcome;

    /// <summary>Dispatch completed locally.</summary>
    public sealed record Completed(Guid OutboxMessageId, DispatchJobOutcome Outcome) : ProcessOutboxOutcome;

    /// <summary>Retry scheduled.</summary>
    public sealed record RetryScheduled(Guid OutboxMessageId, DateTimeOffset At) : ProcessOutboxOutcome;

    /// <summary>Dead-lettered / needs attention.</summary>
    public sealed record DeadLettered(Guid OutboxMessageId, string ReasonCode) : ProcessOutboxOutcome;
}

/// <summary>
/// Claims and processes one outbox dispatch: HTTP outside TX, complete after confirm/reconcile.
/// </summary>
public sealed class ProcessOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IIntegrationStore _store;
    private readonly ICanonicalWriter _canonical;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IWorkOrderDispatcher _dispatcher;
    private readonly IWorkOrderReconciler _reconciler;
    private readonly FailureDispositionPolicy _dispositionPolicy;
    private readonly StructuredAuditWriter _audit;
    private readonly OutboundDispatchOptions _options;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ProcessOutboxMessageHandler(
        IIntegrationStore store,
        ICanonicalWriter canonical,
        IConnectorUnitOfWork unitOfWork,
        IWorkOrderDispatcher dispatcher,
        IWorkOrderReconciler reconciler,
        FailureDispositionPolicy dispositionPolicy,
        StructuredAuditWriter audit,
        IOptions<OutboundDispatchOptions> options,
        IClock clock)
    {
        _store = store;
        _canonical = canonical;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _reconciler = reconciler;
        _dispositionPolicy = dispositionPolicy;
        _audit = audit;
        _options = options.Value;
        _clock = clock;
    }

    /// <summary>
    /// Claim → optional HTTP → complete TX. Never holds DB TX across HTTP.
    /// </summary>
    public async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> HandleAsync(
        string providerInstanceId,
        CancellationToken cancellationToken)
    {
        var claimed = await _store.ClaimNextOutboxMessageAsync(providerInstanceId, _clock.UtcNow, cancellationToken);
        if (claimed is null)
        {
            return Result<ProcessOutboxOutcome, IntegrationFailure>.Ok(new ProcessOutboxOutcome.Idle());
        }

        var messageId = claimed.Id;
        try
        {
            if (!string.Equals(claimed.OperationType, OutboxOperationTypes.DispatchWorkOrder, StringComparison.Ordinal))
            {
                return await DeadLetterAsync(
                    messageId,
                    new IntegrationFailure(
                        FailureCodes.UnsupportedCapability,
                        "Unsupported outbox operation type.",
                        FailureCategory.ProviderContract),
                    cancellationToken);
            }

            // Recovery: prior HTTP succeeded but local completion failed.
            if (!string.IsNullOrWhiteSpace(claimed.ResultReference))
            {
                return await CompleteLocallyAsync(
                    claimed,
                    claimed.ResultReference,
                    correlationId: ExtractCorrelation(claimed),
                    alreadyExisted: true,
                    cancellationToken);
            }

            var existingLink = await _store.FindIdentityByCanonicalAsync(
                providerInstanceId,
                CanonicalEntityTypes.Job,
                claimed.CanonicalId,
                cancellationToken);
            if (existingLink is not null)
            {
                return await CompleteLocallyAsync(
                    claimed,
                    existingLink.ExternalId,
                    correlationId: ExtractCorrelation(claimed),
                    alreadyExisted: true,
                    cancellationToken);
            }

            var payload = JsonSerializer.Deserialize<OutboxDispatchPayload>(claimed.CommandPayload, JsonOptions);
            if (payload?.Command is null)
            {
                return await DeadLetterAsync(
                    messageId,
                    new IntegrationFailure(
                        FailureCodes.MalformedProviderPayload,
                        "Outbox command payload was malformed.",
                        FailureCategory.ProviderContract),
                    cancellationToken);
            }

            // HTTP outside any open database transaction.
            var dispatched = await _dispatcher.DispatchAsync(payload.Command, cancellationToken);
            if (dispatched.IsSuccess)
            {
                var snapshot = ((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)dispatched).Value;
                try
                {
                    return await CompleteLocallyAsync(
                        claimed,
                        snapshot.ExternalWorkOrderId,
                        payload.CorrelationId,
                        alreadyExisted: false,
                        cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Persist external id so the next attempt completes without a duplicate POST.
                    await StashResultReferenceAsync(messageId, snapshot.ExternalWorkOrderId, cancellationToken);
                    throw;
                }
            }

            var providerFailure = ((Result<WorkOrderSnapshot, ProviderFailure>.Failed)dispatched).Error;
            var integrationFailure = ProviderFailureTranslator.ToIntegrationFailure(providerFailure);

            if (providerFailure.Kind == ProviderFailureKind.AmbiguousWrite)
            {
                return await ReconcileAmbiguousAsync(claimed, payload, cancellationToken);
            }

            return await ApplyDispositionAsync(messageId, integrationFailure, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await ApplyDispositionAsync(
                messageId,
                IntegrationFailure.Unexpected("Outbox processing failed unexpectedly."),
                cancellationToken);
        }
    }

    private async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> ReconcileAmbiguousAsync(
        OutboxMessage claimed,
        OutboxDispatchPayload payload,
        CancellationToken cancellationToken)
    {
        var reconciled = await _reconciler.GetByClientReferenceAsync(
            payload.Command.ClientReference,
            cancellationToken);

        if (reconciled.IsSuccess)
        {
            var snapshot = ((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)reconciled).Value;
            return await CompleteLocallyAsync(
                claimed,
                snapshot.ExternalWorkOrderId,
                payload.CorrelationId,
                alreadyExisted: true,
                cancellationToken);
        }

        // Not found: safe to retry POST with the same idempotency key.
        return await ApplyDispositionAsync(
            claimed.Id,
            new IntegrationFailure(
                FailureCodes.AmbiguousProviderWrite,
                "Ambiguous create was not found on reconcile; will retry with the same idempotency key.",
                FailureCategory.Timeout),
            cancellationToken);
    }

    private async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> CompleteLocallyAsync(
        OutboxMessage claimed,
        string externalWorkOrderId,
        string? correlationId,
        bool alreadyExisted,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindOutboxByIdAsync(claimed.Id, cancellationToken);
        if (message is null)
        {
            return Result<ProcessOutboxOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Unexpected("Outbox message disappeared during completion."));
        }

        var job = await _canonical.FindJobAsync(message.CanonicalId, cancellationToken);
        if (job is null)
        {
            return await DeadLetterAsync(
                message.Id,
                new IntegrationFailure(
                    FailureCodes.JobNotFound,
                    "Job missing during outbox completion.",
                    FailureCategory.NotFound),
                cancellationToken);
        }

        var link = await _store.FindIdentityByCanonicalAsync(
            message.ProviderInstanceId,
            CanonicalEntityTypes.Job,
            job.JobId,
            cancellationToken);

        if (link is null)
        {
            await _store.AddIdentityLinkAsync(
                new ProviderIdentityLink
                {
                    Id = Guid.NewGuid(),
                    ProviderName = ProviderNames.FieldFlow,
                    ProviderInstanceId = message.ProviderInstanceId,
                    ExternalEntityType = ExternalEntityTypes.WorkOrder,
                    ExternalId = externalWorkOrderId,
                    CanonicalEntityType = CanonicalEntityTypes.Job,
                    CanonicalId = job.JobId,
                    LastAppliedAt = _clock.UtcNow
                },
                cancellationToken);
        }

        if (string.Equals(job.Status, JobStatuses.Qualified, StringComparison.Ordinal))
        {
            job.Status = JobStatuses.Dispatched;
        }

        message.State = OutboxMessageStates.Completed;
        message.ResultReference = externalWorkOrderId;
        message.NextAttemptAt = null;
        message.ErrorCategory = null;
        message.ErrorMessage = null;
        message.RowVersion += 1;

        await _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                Direction = "outbound",
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = message.ProviderInstanceId,
                Operation = "dispatch.completed",
                CanonicalEntityType = CanonicalEntityTypes.Job,
                CanonicalId = job.JobId,
                EventId = message.IdempotencyKey,
                Result = alreadyExisted ? "already_dispatched" : "dispatched",
                Timestamp = _clock.UtcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        DispatchJobOutcome outcome = alreadyExisted
            ? new DispatchJobOutcome.AlreadyDispatched(job.JobId, externalWorkOrderId)
            : new DispatchJobOutcome.Dispatched(job.JobId, externalWorkOrderId);

        return Result<ProcessOutboxOutcome, IntegrationFailure>.Ok(
            new ProcessOutboxOutcome.Completed(message.Id, outcome));
    }

    private async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> ApplyDispositionAsync(
        Guid messageId,
        IntegrationFailure failure,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindOutboxByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return Result<ProcessOutboxOutcome, IntegrationFailure>.Fail(failure);
        }

        var disposition = _dispositionPolicy.Decide(
            failure,
            new FailureDispositionContext(
                message.AttemptCount,
                message.CreatedAt,
                _clock.UtcNow,
                MaxAttempts: _options.MaxAttempts));

        switch (disposition)
        {
            case FailureDisposition.DeadLetter dead:
                return await PersistDeadLetterAsync(message, failure, dead.ReasonCode, cancellationToken);

            case FailureDisposition.NeedsAttention attention:
                return await PersistDeadLetterAsync(message, failure, attention.ReasonCode, cancellationToken);

            case FailureDisposition.RetryAt retry:
                AppendFailure(message, failure);
                message.State = OutboxMessageStates.Pending;
                message.NextAttemptAt = retry.At;
                message.ErrorCategory = failure.Category.ToString();
                message.ErrorMessage = failure.SafeMessage;
                message.RowVersion += 1;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                ConnectorTelemetry.RecordOutbox("retried");
                return Result<ProcessOutboxOutcome, IntegrationFailure>.Ok(
                    new ProcessOutboxOutcome.RetryScheduled(message.Id, retry.At));

            default:
                AppendFailure(message, failure);
                message.State = OutboxMessageStates.Pending;
                message.NextAttemptAt = _clock.UtcNow.AddSeconds(30);
                message.RowVersion += 1;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                ConnectorTelemetry.RecordOutbox("retried");
                return Result<ProcessOutboxOutcome, IntegrationFailure>.Ok(
                    new ProcessOutboxOutcome.RetryScheduled(message.Id, message.NextAttemptAt.Value));
        }
    }

    private async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> PersistDeadLetterAsync(
        OutboxMessage message,
        IntegrationFailure failure,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var causationId = CorrelationIdRules.NewId();
        AppendFailure(message, failure, causationId);
        message.State = OutboxMessageStates.DeadLettered;
        message.NextAttemptAt = null;
        message.ErrorCategory = failure.Category.ToString();
        message.ErrorMessage = failure.SafeMessage;
        message.RowVersion += 1;
        await _audit.WriteAsync(
            new AuditWriteRequest
            {
                Operation = AuditOperations.DeadLettered,
                Result = "dead_lettered",
                Direction = AuditDirections.Outbound,
                CorrelationId = ExtractCorrelation(message),
                CausationId = causationId,
                ProviderName = message.ProviderName,
                ProviderInstanceId = message.ProviderInstanceId,
                CanonicalEntityType = message.CanonicalEntityType,
                CanonicalId = message.CanonicalId,
                Attempt = message.AttemptCount,
                ErrorCategory = failure.Category.ToString()
            },
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        ConnectorTelemetry.RecordDeadLetter("outbox");
        ConnectorTelemetry.RecordOutbox("dead_lettered");
        return Result<ProcessOutboxOutcome, IntegrationFailure>.Ok(
            new ProcessOutboxOutcome.DeadLettered(message.Id, reasonCode));
    }

    private async Task StashResultReferenceAsync(
        Guid messageId,
        string externalWorkOrderId,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindOutboxByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return;
        }

        message.ResultReference = externalWorkOrderId;
        message.State = OutboxMessageStates.Pending;
        message.NextAttemptAt = _clock.UtcNow;
        message.RowVersion += 1;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<ProcessOutboxOutcome, IntegrationFailure>> DeadLetterAsync(
        Guid messageId,
        IntegrationFailure failure,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindOutboxByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return Result<ProcessOutboxOutcome, IntegrationFailure>.Fail(failure);
        }

        return await PersistDeadLetterAsync(message, failure, failure.Code, cancellationToken);
    }

    private void AppendFailure(OutboxMessage message, IntegrationFailure failure, string? causationId = null) =>
        message.FailureHistoryJson = FailureHistory.Append(
            message.FailureHistoryJson,
            failure,
            message.AttemptCount,
            _clock.UtcNow,
            causationId);

    private static string? ExtractCorrelation(OutboxMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<OutboxDispatchPayload>(message.CommandPayload, JsonOptions)?.CorrelationId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
