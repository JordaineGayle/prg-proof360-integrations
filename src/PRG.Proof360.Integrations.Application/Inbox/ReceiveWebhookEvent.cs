using System.Security.Cryptography;
using System.Text.Json;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Inbox;

/// <summary>
/// Raw inbound webhook receipt command (signature verified inside the handler).
/// </summary>
public sealed class ReceiveWebhookEventCommand
{
    /// <summary>Raw body bytes — must be the exact signing input.</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }

    /// <summary>Signature header.</summary>
    public string? SignatureHeader { get; init; }

    /// <summary>Unix timestamp header.</summary>
    public string? TimestampHeader { get; init; }

    /// <summary>Provider instance header.</summary>
    public string? ProviderInstanceHeader { get; init; }

    /// <summary>Event id header.</summary>
    public string? EventIdHeader { get; init; }

    /// <summary>Event type header.</summary>
    public string? EventTypeHeader { get; init; }

    /// <summary>Schema version header.</summary>
    public string? SchemaVersionHeader { get; init; }

    /// <summary>Entity version header.</summary>
    public string? EntityVersionHeader { get; init; }

    /// <summary>Correlation id.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Configured max body size (enforced by caller; re-checked here).</summary>
    public int MaxBodyBytes { get; init; } = 65_536;
}

/// <summary>
/// Verifies webhook authenticity, records sanitized security telemetry on failure,
/// and durably receives into the inbox without processing.
/// </summary>
public sealed class ReceiveWebhookEventHandler
{
    private readonly IWebhookVerifier _verifier;
    private readonly IInboundWebhookNormalizer _normalizer;
    private readonly IWorkOrderSnapshotSource _workOrders;
    private readonly ReceiveProviderEventHandler _receive;
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ReceiveWebhookEventHandler(
        IWebhookVerifier verifier,
        IInboundWebhookNormalizer normalizer,
        IWorkOrderSnapshotSource workOrders,
        ReceiveProviderEventHandler receive,
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        IClock clock)
    {
        _verifier = verifier;
        _normalizer = normalizer;
        _workOrders = workOrders;
        _receive = receive;
        _store = store;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Verifies then durably receives. Does not process inbox messages.
    /// </summary>
    public async Task<Result<ReceiveEventOutcome, IntegrationFailure>> HandleAsync(
        ReceiveWebhookEventCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.RawBody.Length == 0)
        {
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Validation(FailureCodes.RequiredFieldMissing, "Webhook body is required."));
        }

        if (command.RawBody.Length > command.MaxBodyBytes)
        {
            await WriteSecurityAuditAsync(
                command,
                FailureCodes.WebhookPayloadTooLarge,
                "rejected",
                cancellationToken);
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.WebhookPayloadTooLarge,
                    "Webhook body exceeds the configured size limit.",
                    FailureCategory.Validation));
        }

        var verification = _verifier.Verify(new WebhookVerificationRequest
        {
            RawBody = command.RawBody,
            Signature = command.SignatureHeader,
            TimestampHeader = command.TimestampHeader,
            ProviderInstanceHeader = command.ProviderInstanceHeader,
            EventIdHeader = command.EventIdHeader
        });

        if (!verification.IsValid)
        {
            var failure = MapVerificationFailure(verification);
            await WriteSecurityAuditAsync(command, failure.Code, "rejected", cancellationToken);
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(failure);
        }

        if (string.IsNullOrWhiteSpace(command.EventIdHeader) ||
            string.IsNullOrWhiteSpace(command.EventTypeHeader) ||
            string.IsNullOrWhiteSpace(command.SchemaVersionHeader) ||
            string.IsNullOrWhiteSpace(command.ProviderInstanceHeader) ||
            string.IsNullOrWhiteSpace(command.TimestampHeader) ||
            string.IsNullOrWhiteSpace(command.SignatureHeader))
        {
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                IntegrationFailure.Validation(
                    FailureCodes.RequiredFieldMissing,
                    "Webhook event id, type, schema version, provider instance, timestamp, and signature are required."));
        }

        var normalized = await _normalizer.NormalizeAsync(
            new WebhookNormalizeRequest
            {
                RawBody = command.RawBody,
                EventIdHeader = command.EventIdHeader,
                EventTypeHeader = command.EventTypeHeader,
                SchemaVersionHeader = command.SchemaVersionHeader,
                EntityVersionHeader = command.EntityVersionHeader,
                ProviderInstanceHeader = command.ProviderInstanceHeader
            },
            cancellationToken);

        if (normalized.IsFailure)
        {
            return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                ProviderFailureTranslator.ToIntegrationFailure(
                    ((Result<NormalizedWebhookEvent, ProviderFailure>.Failed)normalized).Error));
        }

        var evt = ((Result<NormalizedWebhookEvent, ProviderFailure>.Succeeded)normalized).Value;

        if (evt.RequiresReconciliation && !string.IsNullOrWhiteSpace(evt.ExternalWorkOrderId))
        {
            // Consistency tradeoff: webhook is a notification; GET is source of current state.
            var reconciled = await _workOrders.GetAsync(evt.ExternalWorkOrderId, cancellationToken);
            if (reconciled.IsFailure)
            {
                return Result<ReceiveEventOutcome, IntegrationFailure>.Fail(
                    ProviderFailureTranslator.ToIntegrationFailure(
                        ((Result<WorkOrderSnapshot, ProviderFailure>.Failed)reconciled).Error));
            }

            var snapshot = ((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)reconciled).Value;
            var envelope = JsonSerializer.Serialize(snapshot);
            return await _receive.HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = snapshot.ProviderName,
                    ProviderInstanceId = snapshot.ProviderInstanceId,
                    EventId = evt.EventId,
                    EventType = InboxEventTypes.WorkOrderStatusChanged,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(snapshot),
                    EventVersion = snapshot.EntityVersion,
                    SchemaVersion = snapshot.SchemaVersion ?? evt.SchemaVersion,
                    OccurredAt = snapshot.OccurredAt ?? evt.OccurredAt,
                    CorrelationId = command.CorrelationId
                },
                cancellationToken);
        }

        return await _receive.HandleAsync(
            new ReceiveProviderEventCommand
            {
                ProviderName = evt.ProviderName,
                ProviderInstanceId = evt.ProviderInstanceId,
                EventId = evt.EventId,
                EventType = evt.IsSupported ? evt.InboxEventType : evt.OriginalEventType,
                PayloadEnvelope = evt.PayloadEnvelope,
                PayloadHash = evt.PayloadHash,
                EventVersion = evt.EntityVersion,
                SchemaVersion = evt.SchemaVersion,
                OccurredAt = evt.OccurredAt,
                CorrelationId = command.CorrelationId
            },
            cancellationToken);
    }

    private static IntegrationFailure MapVerificationFailure(WebhookVerificationResult verification)
    {
        var code = verification.FailureCode ?? FailureCodes.WebhookSignatureInvalid;
        return code switch
        {
            "timestamp_skew" or "invalid_timestamp" => new IntegrationFailure(
                FailureCodes.WebhookTimestampSkew,
                "Webhook timestamp is outside the allowed replay window.",
                FailureCategory.Unauthorized),
            "missing_signature" => new IntegrationFailure(
                FailureCodes.WebhookSignatureInvalid,
                "Webhook signature and timestamp are required.",
                FailureCategory.Unauthorized),
            "provider_instance_mismatch" => new IntegrationFailure(
                FailureCodes.WebhookSignatureInvalid,
                "Webhook provider instance does not match configuration.",
                FailureCategory.Unauthorized),
            "misconfigured" => new IntegrationFailure(
                FailureCodes.UnexpectedError,
                "Webhook verification is not configured.",
                FailureCategory.Unexpected),
            _ => new IntegrationFailure(
                FailureCodes.WebhookSignatureInvalid,
                "Webhook signature verification failed.",
                FailureCategory.Unauthorized)
        };
    }

    private async Task WriteSecurityAuditAsync(
        ReceiveWebhookEventCommand command,
        string failureCode,
        string result,
        CancellationToken cancellationToken)
    {
        // Sanitized only: never raw body, signature, or secret.
        var bodyHash = Convert.ToHexString(SHA256.HashData(command.RawBody.Span)).ToLowerInvariant();
        await _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = command.CorrelationId,
                Direction = "inbound",
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = SanitizeInstance(command.ProviderInstanceHeader),
                Operation = "webhook.verify",
                EventId = SanitizeToken(command.EventIdHeader),
                Result = result,
                ErrorCategory = failureCode,
                PayloadHash = bodyHash,
                Timestamp = _clock.UtcNow
            },
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string? SanitizeInstance(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128
            ? null
            : value.Trim();

    private static string? SanitizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 256
            ? null
            : value.Trim();
}
