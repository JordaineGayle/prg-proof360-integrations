using System.Text.Json;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Dispatch;

/// <summary>
/// Typed success for queuing a dispatch (no provider HTTP).
/// </summary>
public abstract record QueueDispatchOutcome
{
    private QueueDispatchOutcome()
    {
    }

    /// <summary>Outbox row created.</summary>
    public sealed record Queued(Guid OutboxMessageId, string IdempotencyKey) : QueueDispatchOutcome;

    /// <summary>Existing outbox row returned (idempotent).</summary>
    public sealed record AlreadyQueued(Guid OutboxMessageId, string State, string IdempotencyKey) : QueueDispatchOutcome;

    /// <summary>Job already linked/dispatched.</summary>
    public sealed record AlreadyDispatched(Guid JobId, string ExternalWorkOrderId) : QueueDispatchOutcome;
}

/// <summary>
/// Queues a qualified Job for FieldFlow dispatch via transactional outbox.
/// </summary>
public sealed class QueueJobDispatchHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IPersistenceExceptionClassifier _exceptions;
    private readonly IProviderCapabilities _capabilities;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public QueueJobDispatchHandler(
        ICanonicalWriter canonical,
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        IPersistenceExceptionClassifier exceptions,
        IProviderCapabilities capabilities,
        IClock clock)
    {
        _canonical = canonical;
        _store = store;
        _unitOfWork = unitOfWork;
        _exceptions = exceptions;
        _capabilities = capabilities;
        _clock = clock;
    }

    /// <summary>
    /// Validates eligibility, inserts outbox + audit, commits. No FieldFlow HTTP.
    /// </summary>
    public async Task<Result<QueueDispatchOutcome, IntegrationFailure>> HandleAsync(
        Guid jobId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (!_capabilities.Supports(ProviderCapability.WorkOrderDispatch))
        {
            return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.UnsupportedCapability,
                    "Provider does not support work-order dispatch.",
                    FailureCategory.ProviderContract));
        }

        var job = await _canonical.FindJobAsync(jobId, cancellationToken);
        if (job is null)
        {
            return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.JobNotFound,
                    "Job was not found.",
                    FailureCategory.NotFound));
        }

        var existingLink = await _store.FindIdentityByCanonicalAsync(
            _capabilities.ProviderInstanceId,
            CanonicalEntityTypes.Job,
            jobId,
            cancellationToken);
        if (existingLink is not null)
        {
            return Result<QueueDispatchOutcome, IntegrationFailure>.Ok(
                new QueueDispatchOutcome.AlreadyDispatched(jobId, existingLink.ExternalId));
        }

        var idempotencyKey = DispatchIdempotencyKeys.ForJob(_capabilities.ProviderInstanceId, jobId);
        var existingOutbox = await _store.FindOutboxByIdempotencyKeyAsync(
            _capabilities.ProviderInstanceId,
            idempotencyKey,
            cancellationToken);
        if (existingOutbox is not null)
        {
            var incomingHash = BuildCommandHash(job, await ResolveContractorIdAsync(job, cancellationToken), idempotencyKey);
            var existingHash = ExtractPayloadHash(existingOutbox.CommandPayload);
            if (existingHash is not null &&
                !string.Equals(existingHash, incomingHash, StringComparison.Ordinal) &&
                existingOutbox.State is not OutboxMessageStates.Completed)
            {
                return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(
                    new IntegrationFailure(
                        FailureCodes.IdempotencyKeyConflict,
                        "Dispatch idempotency key already exists with a different logical payload.",
                        FailureCategory.Conflict));
            }

            if (existingOutbox.State == OutboxMessageStates.Completed &&
                !string.IsNullOrWhiteSpace(existingOutbox.ResultReference))
            {
                return Result<QueueDispatchOutcome, IntegrationFailure>.Ok(
                    new QueueDispatchOutcome.AlreadyDispatched(jobId, existingOutbox.ResultReference));
            }

            return Result<QueueDispatchOutcome, IntegrationFailure>.Ok(
                new QueueDispatchOutcome.AlreadyQueued(
                    existingOutbox.Id,
                    existingOutbox.State,
                    existingOutbox.IdempotencyKey));
        }

        Vendor? vendor = null;
        if (job.AssignedVendorId is { } vendorId)
        {
            vendor = await _canonical.FindVendorAsync(vendorId, cancellationToken);
        }

        var gate = JobDispatchEligibility.Evaluate(job, vendor);
        if (gate is not null)
        {
            return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(gate);
        }

        var contractorId = await ResolveContractorIdAsync(job, cancellationToken);
        if (string.IsNullOrWhiteSpace(contractorId))
        {
            return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.ContractorMappingMissing,
                    "Assigned Vendor has no FieldFlow contractor identity link.",
                    FailureCategory.Dependency));
        }

        var command = BuildCommand(job, contractorId, idempotencyKey);
        var payloadHash = SyntheticEventIds.HashPayload(command);
        var envelope = JsonSerializer.Serialize(new OutboxDispatchPayload
        {
            Command = command,
            PayloadHash = payloadHash,
            CorrelationId = correlationId
        });

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = _capabilities.ProviderInstanceId,
            OperationType = OutboxOperationTypes.DispatchWorkOrder,
            IdempotencyKey = idempotencyKey,
            CanonicalEntityType = CanonicalEntityTypes.Job,
            CanonicalId = jobId,
            CommandPayload = envelope,
            State = OutboxMessageStates.Pending,
            AttemptCount = 0,
            CreatedAt = _clock.UtcNow
        };

        await _store.AddOutboxMessageAsync(outbox, cancellationToken);
        await _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                Direction = "outbound",
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = _capabilities.ProviderInstanceId,
                Operation = "dispatch.requested",
                CanonicalEntityType = CanonicalEntityTypes.Job,
                CanonicalId = jobId,
                EventId = idempotencyKey,
                Result = "queued",
                PayloadHash = payloadHash,
                Timestamp = _clock.UtcNow
            },
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_exceptions.IsUniqueConstraintViolation(ex))
        {
            var duplicate = await _store.FindOutboxByIdempotencyKeyAsync(
                _capabilities.ProviderInstanceId,
                idempotencyKey,
                cancellationToken);
            if (duplicate is null)
            {
                return Result<QueueDispatchOutcome, IntegrationFailure>.Fail(
                    new IntegrationFailure(
                        FailureCodes.ConcurrencyConflict,
                        "Outbox uniqueness conflict could not be resolved.",
                        FailureCategory.PersistenceConflict));
            }

            return Result<QueueDispatchOutcome, IntegrationFailure>.Ok(
                new QueueDispatchOutcome.AlreadyQueued(duplicate.Id, duplicate.State, duplicate.IdempotencyKey));
        }

        return Result<QueueDispatchOutcome, IntegrationFailure>.Ok(
            new QueueDispatchOutcome.Queued(outbox.Id, idempotencyKey));
    }

    private async Task<string?> ResolveContractorIdAsync(Job job, CancellationToken cancellationToken)
    {
        if (job.AssignedVendorId is null)
        {
            return null;
        }

        var link = await _store.FindIdentityByCanonicalAsync(
            _capabilities.ProviderInstanceId,
            CanonicalEntityTypes.Vendor,
            job.AssignedVendorId.Value,
            cancellationToken);
        return link?.ExternalId;
    }

    private static DispatchWorkOrderCommand BuildCommand(Job job, string contractorId, string idempotencyKey) =>
        new()
        {
            IdempotencyKey = idempotencyKey,
            ClientReference = job.JobId.ToString("D"),
            ExternalContractorId = contractorId,
            CustomerName = job.CustomerName!.Trim(),
            CustomerPhone = TrimOrNull(job.CustomerPhone),
            CustomerEmail = TrimOrNull(job.CustomerEmail),
            AddressStreet = job.AddressStreet!.Trim(),
            AddressUnit = TrimOrNull(job.AddressUnit),
            AddressCity = job.AddressCity!.Trim(),
            AddressPostal = TrimOrNull(job.AddressPostal),
            ServiceType = job.ServiceType!.Trim(),
            Subcategory = TrimOrNull(job.Subcategory),
            WindowStart = job.WindowStart,
            WindowEnd = job.WindowEnd,
            Notes = TrimOrNull(job.NotesScope)
        };

    private static string BuildCommandHash(Job job, string? contractorId, string idempotencyKey) =>
        string.IsNullOrWhiteSpace(contractorId)
            ? string.Empty
            : SyntheticEventIds.HashPayload(BuildCommand(job, contractorId, idempotencyKey));

    private static string? ExtractPayloadHash(string commandPayload)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<OutboxDispatchPayload>(commandPayload);
            return payload?.PayloadHash;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Serialized outbox command envelope (not a provider DTO).
/// </summary>
public sealed class OutboxDispatchPayload
{
    /// <summary>Provider-neutral dispatch command.</summary>
    public required DispatchWorkOrderCommand Command { get; init; }

    /// <summary>Hash of the logical command payload.</summary>
    public required string PayloadHash { get; init; }

    /// <summary>Correlation id captured at queue time.</summary>
    public string? CorrelationId { get; init; }
}
