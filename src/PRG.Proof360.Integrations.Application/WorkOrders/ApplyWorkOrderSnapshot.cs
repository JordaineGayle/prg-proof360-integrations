using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.WorkOrders;

/// <summary>
/// Stages canonical Job + identity link changes from a work-order snapshot.
/// Returns a dependency failure when the contractor identity is unknown (no partial Job).
/// </summary>
public sealed class ApplyWorkOrderSnapshotHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly WorkOrderToJobMapper _mapper;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ApplyWorkOrderSnapshotHandler(
        ICanonicalWriter canonical,
        IIntegrationStore store,
        WorkOrderToJobMapper mapper,
        IClock clock)
    {
        _canonical = canonical;
        _store = store;
        _mapper = mapper;
        _clock = clock;
    }

    /// <summary>
    /// Stages Job/link upsert. Does not save changes.
    /// </summary>
    public async Task<Result<ApplyWorkOrderOutcome, IntegrationFailure>> HandleAsync(
        WorkOrderSnapshot snapshot,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Guid? assignedVendorId = null;
        if (!string.IsNullOrWhiteSpace(snapshot.ExternalContractorId))
        {
            var contractorLink = await _store.FindIdentityByExternalAsync(
                snapshot.ProviderInstanceId,
                ExternalEntityTypes.Contractor,
                snapshot.ExternalContractorId,
                cancellationToken);
            if (contractorLink is null)
            {
                return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Fail(
                    new IntegrationFailure(
                        FailureCodes.ContractorMappingMissing,
                        "Contractor identity is not mapped yet.",
                        FailureCategory.Dependency));
            }

            assignedVendorId = contractorLink.CanonicalId;
        }

        var link = await _store.FindIdentityByExternalAsync(
            snapshot.ProviderInstanceId,
            ExternalEntityTypes.WorkOrder,
            snapshot.ExternalWorkOrderId,
            cancellationToken);

        var incomingHash = SyntheticEventIds.HashPayload(snapshot);

        if (link is not null && link.LastAppliedVersion is { } applied)
        {
            if (snapshot.EntityVersion < applied)
            {
                await WriteAuditAsync(
                    snapshot,
                    correlationId,
                    link.CanonicalId,
                    "ignored_stale",
                    cancellationToken,
                    incomingHash);
                return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                    new ApplyWorkOrderOutcome.IgnoredStale(link.CanonicalId, applied));
            }

            if (snapshot.EntityVersion == applied)
            {
                if (!string.IsNullOrWhiteSpace(link.PayloadHash) &&
                    !string.Equals(link.PayloadHash, incomingHash, StringComparison.Ordinal))
                {
                    await WriteAuditAsync(
                        snapshot,
                        correlationId,
                        link.CanonicalId,
                        "version_payload_conflict",
                        cancellationToken,
                        incomingHash,
                        FailureCodes.VersionPayloadConflict);
                    return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                        new ApplyWorkOrderOutcome.VersionPayloadConflict(link.CanonicalId, applied));
                }

                await WriteAuditAsync(
                    snapshot,
                    correlationId,
                    link.CanonicalId,
                    "ignored_stale",
                    cancellationToken,
                    incomingHash);
                return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                    new ApplyWorkOrderOutcome.IgnoredStale(link.CanonicalId, applied));
            }
        }

        if (link is null)
        {
            return await StageCreateAsync(snapshot, assignedVendorId, correlationId, incomingHash, cancellationToken);
        }

        return await StageUpdateAsync(snapshot, link, assignedVendorId, correlationId, incomingHash, cancellationToken);
    }

    private async Task<Result<ApplyWorkOrderOutcome, IntegrationFailure>> StageCreateAsync(
        WorkOrderSnapshot snapshot,
        Guid? assignedVendorId,
        string? correlationId,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var jobId = Guid.TryParse(snapshot.ClientReference, out var parsed)
            ? parsed
            : Guid.NewGuid();

        var mapped = _mapper.MapNewFromProvider(snapshot, jobId, assignedVendorId);
        if (!mapped.IsSuccess)
        {
            return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.UnknownProviderStatus,
                    mapped.Error ?? "Work-order mapping failed.",
                    FailureCategory.ProviderContract));
        }

        var link = new ProviderIdentityLink
        {
            Id = Guid.NewGuid(),
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = snapshot.ProviderInstanceId,
            ExternalEntityType = ExternalEntityTypes.WorkOrder,
            ExternalId = snapshot.ExternalWorkOrderId,
            CanonicalEntityType = CanonicalEntityTypes.Job,
            CanonicalId = mapped.Job!.JobId,
            LastAppliedVersion = snapshot.EntityVersion,
            LastAppliedAt = _clock.UtcNow,
            PayloadHash = payloadHash
        };

        await _canonical.AddJobAsync(mapped.Job, cancellationToken);
        await _store.AddIdentityLinkAsync(link, cancellationToken);
        await WriteAuditAsync(snapshot, correlationId, mapped.Job.JobId, "created", cancellationToken, payloadHash);
        return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
            new ApplyWorkOrderOutcome.Created(mapped.Job.JobId));
    }

    private async Task<Result<ApplyWorkOrderOutcome, IntegrationFailure>> StageUpdateAsync(
        WorkOrderSnapshot snapshot,
        ProviderIdentityLink link,
        Guid? assignedVendorId,
        string? correlationId,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var job = await _canonical.FindJobAsync(link.CanonicalId, cancellationToken);
        if (job is null)
        {
            return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.JobNotFound,
                    "Identity link pointed at a missing Job.",
                    FailureCategory.NotFound));
        }

        var before = HashJob(job);
        var mapped = _mapper.MergeUpdate(job, snapshot, assignedVendorId);
        if (!mapped.IsSuccess)
        {
            await WriteAuditAsync(
                snapshot,
                correlationId,
                job.JobId,
                "ignored_invalid_transition",
                cancellationToken,
                payloadHash);
            return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                new ApplyWorkOrderOutcome.IgnoredInvalidTransition(job.JobId, mapped.Error ?? "invalid_transition"));
        }

        link.LastAppliedVersion = snapshot.EntityVersion;
        link.LastAppliedAt = _clock.UtcNow;
        link.PayloadHash = payloadHash;
        link.RowVersion += 1;

        var after = HashJob(mapped.Job!);
        if (before == after && mapped.IgnoredOwnershipFields.Count == 0)
        {
            await WriteAuditAsync(snapshot, correlationId, job.JobId, "no_change", cancellationToken, payloadHash);
            return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                new ApplyWorkOrderOutcome.NoChange(job.JobId));
        }

        await WriteAuditAsync(snapshot, correlationId, job.JobId, "updated", cancellationToken, payloadHash);
        return Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
            new ApplyWorkOrderOutcome.Updated(job.JobId));
    }

    private Task WriteAuditAsync(
        WorkOrderSnapshot snapshot,
        string? correlationId,
        Guid jobId,
        string result,
        CancellationToken cancellationToken,
        string? payloadHash = null,
        string? errorCategory = null) =>
        _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                Direction = "inbound",
                ProviderName = snapshot.ProviderName,
                ProviderInstanceId = snapshot.ProviderInstanceId,
                Operation = "work_order.apply",
                CanonicalEntityType = CanonicalEntityTypes.Job,
                CanonicalId = jobId,
                EventId = snapshot.ExternalWorkOrderId,
                Result = result,
                ErrorCategory = errorCategory,
                SchemaVersion = snapshot.SchemaVersion,
                PayloadHash = payloadHash,
                Timestamp = _clock.UtcNow
            },
            cancellationToken);

    private static string HashJob(Domain.Canonical.Job job) =>
        $"{job.Status}|{job.CustomerName}|{job.AddressCity}|{job.ServiceType}|{job.WindowStart}|{job.WindowEnd}|{job.NotesScope}|{job.AssignedVendorId}|{job.Priority}|{job.ComplianceOnly}";
}
