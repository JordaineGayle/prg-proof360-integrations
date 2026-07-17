using System.Text.Json;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Application.WorkOrders;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Inbox;

/// <summary>
/// Outcome of processing one inbox message.
/// </summary>
public abstract record ProcessInboxOutcome
{
    private ProcessInboxOutcome()
    {
    }

    /// <summary>No eligible message.</summary>
    public sealed record Idle : ProcessInboxOutcome;

    /// <summary>Contractor snapshot applied.</summary>
    public sealed record ContractorApplied(Guid InboxMessageId, ApplyContractorOutcome Outcome) : ProcessInboxOutcome;

    /// <summary>Work-order snapshot applied.</summary>
    public sealed record WorkOrderApplied(Guid InboxMessageId, ApplyWorkOrderOutcome Outcome) : ProcessInboxOutcome;

    /// <summary>Waiting for contractor dependency.</summary>
    public sealed record WaitingForDependency(Guid InboxMessageId, string DependencyCode) : ProcessInboxOutcome;

    /// <summary>Dead-lettered.</summary>
    public sealed record DeadLettered(Guid InboxMessageId, string ReasonCode) : ProcessInboxOutcome;
}

/// <summary>
/// Claims and processes one inbox message through the shared apply path.
/// </summary>
public sealed class ProcessInboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly ApplyContractorSnapshotHandler _contractors;
    private readonly ApplyWorkOrderSnapshotHandler _workOrders;
    private readonly FailureDispositionPolicy _dispositionPolicy;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ProcessInboxMessageHandler(
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        ApplyContractorSnapshotHandler contractors,
        ApplyWorkOrderSnapshotHandler workOrders,
        FailureDispositionPolicy dispositionPolicy,
        IClock clock)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _contractors = contractors;
        _workOrders = workOrders;
        _dispositionPolicy = dispositionPolicy;
        _clock = clock;
    }

    /// <summary>
    /// Claim TX then apply TX. Provider HTTP is never called here.
    /// </summary>
    public async Task<Result<ProcessInboxOutcome, IntegrationFailure>> HandleAsync(
        string providerInstanceId,
        CancellationToken cancellationToken)
    {
        var claimed = await _store.ClaimNextInboxMessageAsync(providerInstanceId, _clock.UtcNow, cancellationToken);
        if (claimed is null)
        {
            return Result<ProcessInboxOutcome, IntegrationFailure>.Ok(new ProcessInboxOutcome.Idle());
        }

        var messageId = claimed.Id;
        try
        {
            return claimed.EventType switch
            {
                InboxEventTypes.ContractorSnapshot => await ProcessContractorAsync(claimed, cancellationToken),
                InboxEventTypes.WorkOrderSnapshot => await ProcessWorkOrderAsync(claimed, cancellationToken),
                InboxEventTypes.WorkOrderStatusChanged => await ProcessWorkOrderAsync(claimed, cancellationToken),
                _ => await DeadLetterAsync(
                    messageId,
                    new IntegrationFailure(
                        FailureCodes.UnsupportedEventType,
                        "Unsupported inbox event type or schema.",
                        FailureCategory.ProviderContract),
                    cancellationToken)
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await ApplyDispositionAsync(
                messageId,
                IntegrationFailure.Unexpected("Inbox processing failed unexpectedly."),
                cancellationToken);
        }
    }

    private async Task<Result<ProcessInboxOutcome, IntegrationFailure>> ProcessContractorAsync(
        InboxMessage message,
        CancellationToken cancellationToken)
    {
        ContractorSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ContractorSnapshot>(message.PayloadEnvelope, JsonOptions);
        }
        catch (JsonException)
        {
            return await DeadLetterAsync(
                message.Id,
                new IntegrationFailure(
                    FailureCodes.MalformedProviderPayload,
                    "Contractor snapshot payload was malformed.",
                    FailureCategory.ProviderContract),
                cancellationToken);
        }

        if (snapshot is null)
        {
            return await DeadLetterAsync(
                message.Id,
                new IntegrationFailure(
                    FailureCodes.MalformedProviderPayload,
                    "Contractor snapshot payload was malformed.",
                    FailureCategory.ProviderContract),
                cancellationToken);
        }

        var applied = await _contractors.HandleAsync(snapshot, message.CorrelationId, cancellationToken);
        if (applied.IsFailure)
        {
            var error = ((Result<ApplyContractorOutcome, IntegrationFailure>.Failed)applied).Error;
            return await ApplyDispositionAsync(message.Id, error, cancellationToken);
        }

        message.State = InboxMessageStates.Completed;
        message.NextAttemptAt = null;
        message.ErrorCategory = null;
        message.ErrorMessage = null;
        message.RowVersion += 1;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProcessInboxOutcome, IntegrationFailure>.Ok(
            new ProcessInboxOutcome.ContractorApplied(
                message.Id,
                ((Result<ApplyContractorOutcome, IntegrationFailure>.Succeeded)applied).Value));
    }

    private async Task<Result<ProcessInboxOutcome, IntegrationFailure>> ProcessWorkOrderAsync(
        InboxMessage message,
        CancellationToken cancellationToken)
    {
        WorkOrderSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<WorkOrderSnapshot>(message.PayloadEnvelope, JsonOptions);
        }
        catch (JsonException)
        {
            return await DeadLetterAsync(
                message.Id,
                new IntegrationFailure(
                    FailureCodes.MalformedProviderPayload,
                    "Work-order snapshot payload was malformed.",
                    FailureCategory.ProviderContract),
                cancellationToken);
        }

        if (snapshot is null)
        {
            return await DeadLetterAsync(
                message.Id,
                new IntegrationFailure(
                    FailureCodes.MalformedProviderPayload,
                    "Work-order snapshot payload was malformed.",
                    FailureCategory.ProviderContract),
                cancellationToken);
        }

        var applied = await _workOrders.HandleAsync(snapshot, message.CorrelationId, cancellationToken);
        if (applied.IsFailure)
        {
            var error = ((Result<ApplyWorkOrderOutcome, IntegrationFailure>.Failed)applied).Error;
            return await ApplyDispositionAsync(message.Id, error, cancellationToken);
        }

        message.State = InboxMessageStates.Completed;
        message.NextAttemptAt = null;
        message.ErrorCategory = null;
        message.ErrorMessage = null;
        message.RowVersion += 1;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProcessInboxOutcome, IntegrationFailure>.Ok(
            new ProcessInboxOutcome.WorkOrderApplied(
                message.Id,
                ((Result<ApplyWorkOrderOutcome, IntegrationFailure>.Succeeded)applied).Value));
    }

    private async Task<Result<ProcessInboxOutcome, IntegrationFailure>> ApplyDispositionAsync(
        Guid messageId,
        IntegrationFailure failure,
        CancellationToken cancellationToken)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindInboxByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return Result<ProcessInboxOutcome, IntegrationFailure>.Fail(failure);
        }

        var disposition = _dispositionPolicy.Decide(
            failure,
            new FailureDispositionContext(
                message.AttemptCount,
                message.ReceivedAt,
                _clock.UtcNow));

        switch (disposition)
        {
            case FailureDisposition.WaitForDependency wait:
                message.State = InboxMessageStates.WaitingForDependency;
                message.NextAttemptAt = wait.At;
                message.ErrorCategory = failure.Category.ToString();
                message.ErrorMessage = failure.SafeMessage;
                message.RowVersion += 1;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<ProcessInboxOutcome, IntegrationFailure>.Ok(
                    new ProcessInboxOutcome.WaitingForDependency(message.Id, wait.DependencyCode));

            case FailureDisposition.DeadLetter dead:
                return await DeadLetterAsync(messageId, failure, cancellationToken, dead.ReasonCode);

            case FailureDisposition.NeedsAttention attention:
                return await DeadLetterAsync(messageId, failure, cancellationToken, attention.ReasonCode);

            case FailureDisposition.RetryAt retry:
                message.State = InboxMessageStates.Pending;
                message.NextAttemptAt = retry.At;
                message.ErrorCategory = failure.Category.ToString();
                message.ErrorMessage = failure.SafeMessage;
                message.RowVersion += 1;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<ProcessInboxOutcome, IntegrationFailure>.Fail(failure);

            default:
                message.State = InboxMessageStates.Pending;
                message.NextAttemptAt = _clock.UtcNow.AddSeconds(30);
                message.RowVersion += 1;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<ProcessInboxOutcome, IntegrationFailure>.Fail(failure);
        }
    }

    private async Task<Result<ProcessInboxOutcome, IntegrationFailure>> DeadLetterAsync(
        Guid messageId,
        IntegrationFailure failure,
        CancellationToken cancellationToken,
        string? reasonCode = null)
    {
        _unitOfWork.ClearTrackedChanges();
        var message = await _store.FindInboxByIdAsync(messageId, cancellationToken);
        if (message is null)
        {
            return Result<ProcessInboxOutcome, IntegrationFailure>.Fail(failure);
        }

        message.State = InboxMessageStates.DeadLettered;
        message.NextAttemptAt = null;
        message.ErrorCategory = failure.Category.ToString();
        message.ErrorMessage = failure.SafeMessage;
        message.RowVersion += 1;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ProcessInboxOutcome, IntegrationFailure>.Ok(
            new ProcessInboxOutcome.DeadLettered(message.Id, reasonCode ?? failure.Code));
    }
}
