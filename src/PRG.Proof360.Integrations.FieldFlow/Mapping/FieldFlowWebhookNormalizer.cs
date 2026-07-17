using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow.Mapping;

/// <summary>
/// FieldFlow ACL: verified webhook body → provider-neutral inbox envelope.
/// </summary>
public sealed class FieldFlowWebhookNormalizer : IInboundWebhookNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly FieldFlowWorkOrderMapper _workOrders;

    /// <summary>Creates the normalizer.</summary>
    public FieldFlowWebhookNormalizer(FieldFlowWorkOrderMapper workOrders) => _workOrders = workOrders;

    /// <inheritdoc />
    public Task<Result<NormalizedWebhookEvent, ProviderFailure>> NormalizeAsync(
        WebhookNormalizeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawText = Encoding.UTF8.GetString(request.RawBody.Span);
        var bodyHash = Convert.ToHexString(SHA256.HashData(request.RawBody.Span)).ToLowerInvariant();

        FieldFlowWebhookDto? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<FieldFlowWebhookDto>(rawText, JsonOptions);
        }
        catch (JsonException)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                new ProviderFailure(
                    ProviderFailureKind.ContractViolation,
                    "malformed_provider_payload",
                    "Webhook JSON could not be parsed.")));
        }

        if (envelope is null)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                new ProviderFailure(
                    ProviderFailureKind.ContractViolation,
                    "malformed_provider_payload",
                    "Webhook JSON was empty.")));
        }

        var eventId = FirstNonEmpty(request.EventIdHeader, envelope.EventId);
        var eventType = FirstNonEmpty(request.EventTypeHeader, envelope.EventType);
        var schemaVersion = FirstNonEmpty(request.SchemaVersionHeader, envelope.SchemaVersion);
        var providerInstance = FirstNonEmpty(request.ProviderInstanceHeader, envelope.ProviderInstanceId);
        var entityVersion = ResolveEntityVersion(request.EntityVersionHeader, envelope);

        if (string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(schemaVersion) ||
            string.IsNullOrWhiteSpace(providerInstance) ||
            envelope.OccurredAt is null)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                ProviderFailure.Validation(
                    "required_field_missing",
                    "Webhook event id, type, schema version, provider instance, and occurredAt are required.")));
        }

        var supportedType = string.Equals(eventType, InboxEventTypes.WorkOrderStatusChanged, StringComparison.Ordinal);
        var supportedSchema = string.Equals(schemaVersion, WebhookSchemaVersions.V1, StringComparison.Ordinal);

        if (!supportedType || !supportedSchema)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Ok(new NormalizedWebhookEvent
            {
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = providerInstance,
                EventId = eventId,
                OriginalEventType = eventType,
                InboxEventType = eventType,
                SchemaVersion = schemaVersion,
                EntityVersion = entityVersion,
                OccurredAt = envelope.OccurredAt.Value.ToUniversalTime(),
                PayloadEnvelope = rawText,
                PayloadHash = bodyHash,
                IsSupported = false,
                RequiresReconciliation = false,
                ExternalWorkOrderId = envelope.Data?.WorkOrderId
            }));
        }

        if (envelope.Data is null)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                ProviderFailure.Validation("required_field_missing", "Webhook data payload is required.")));
        }

        if (entityVersion is null or <= 0)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Ok(new NormalizedWebhookEvent
            {
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = providerInstance,
                EventId = eventId,
                OriginalEventType = eventType,
                InboxEventType = InboxEventTypes.WorkOrderStatusChanged,
                SchemaVersion = schemaVersion,
                EntityVersion = null,
                OccurredAt = envelope.OccurredAt.Value.ToUniversalTime(),
                PayloadEnvelope = rawText,
                PayloadHash = bodyHash,
                IsSupported = true,
                RequiresReconciliation = true,
                ExternalWorkOrderId = envelope.Data.WorkOrderId
            }));
        }

        // Prefer envelope entity version when data omits/zeros it.
        if (envelope.Data.EntityVersion <= 0)
        {
            envelope.Data.EntityVersion = entityVersion.Value;
        }

        var mapped = _workOrders.ToSnapshot(
            envelope.Data,
            providerInstance,
            schemaVersion,
            envelope.OccurredAt);
        if (mapped.IsFailure)
        {
            return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                ((Result<WorkOrderSnapshot, ProviderFailure>.Failed)mapped).Error));
        }

        var snapshot = ((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)mapped).Value with
        {
            EntityVersion = entityVersion.Value
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();

        return Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Ok(new NormalizedWebhookEvent
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = providerInstance,
            EventId = eventId,
            OriginalEventType = eventType,
            InboxEventType = InboxEventTypes.WorkOrderStatusChanged,
            SchemaVersion = schemaVersion,
            EntityVersion = entityVersion,
            OccurredAt = envelope.OccurredAt.Value.ToUniversalTime(),
            PayloadEnvelope = snapshotJson,
            PayloadHash = snapshotHash,
            IsSupported = true,
            RequiresReconciliation = false,
            ExternalWorkOrderId = snapshot.ExternalWorkOrderId
        }));
    }

    private static long? ResolveEntityVersion(string? header, FieldFlowWebhookDto envelope)
    {
        if (!string.IsNullOrWhiteSpace(header) && long.TryParse(header, out var fromHeader) && fromHeader > 0)
        {
            return fromHeader;
        }

        if (envelope.EntityVersion > 0)
        {
            return envelope.EntityVersion;
        }

        if (envelope.Data?.EntityVersion > 0)
        {
            return envelope.Data.EntityVersion;
        }

        return null;
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a.Trim() : string.IsNullOrWhiteSpace(b) ? null : b.Trim();
}
