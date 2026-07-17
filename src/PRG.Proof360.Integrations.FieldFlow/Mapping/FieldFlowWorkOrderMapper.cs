using System.Text.Json;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow.Mapping;

/// <summary>
/// Converts FieldFlow work-order DTOs into provider-neutral snapshots.
/// </summary>
public sealed class FieldFlowWorkOrderMapper
{
    /// <summary>
    /// Maps a work-order DTO to a snapshot.
    /// </summary>
    public Result<WorkOrderSnapshot, ProviderFailure> ToSnapshot(
        FieldFlowWorkOrderDto dto,
        string providerInstanceId,
        string? schemaVersion = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.WorkOrderId))
        {
            return Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation("missing_work_order_id", "workOrderId is required."));
        }

        if (string.IsNullOrWhiteSpace(dto.Status))
        {
            return Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation("missing_status", "status is required."));
        }

        try
        {
            _ = dto.WindowStart?.ToUniversalTime();
            _ = dto.WindowEnd?.ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation(
                    "invalid_timestamp",
                    "windowStart/windowEnd could not be normalized to UTC."));
        }

        var unknown = CollectUnknown(dto.ExtensionData);

        return Result<WorkOrderSnapshot, ProviderFailure>.Ok(new WorkOrderSnapshot
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = providerInstanceId,
            ExternalWorkOrderId = dto.WorkOrderId.Trim(),
            ClientReference = TrimOrNull(dto.ClientReference),
            ExternalContractorId = TrimOrNull(dto.ContractorId),
            ProviderStatus = dto.Status.Trim(),
            EntityVersion = dto.EntityVersion,
            SchemaVersion = schemaVersion,
            CustomerName = TrimOrNull(dto.CustomerName),
            CustomerPhone = TrimOrNull(dto.CustomerPhone),
            CustomerEmail = TrimOrNull(dto.CustomerEmail),
            AddressStreet = TrimOrNull(dto.AddressStreet),
            AddressUnit = TrimOrNull(dto.AddressUnit),
            AddressCity = TrimOrNull(dto.AddressCity),
            AddressPostal = TrimOrNull(dto.AddressPostal),
            ServiceType = TrimOrNull(dto.ServiceType),
            Subcategory = TrimOrNull(dto.Subcategory),
            WindowStart = dto.WindowStart?.ToUniversalTime(),
            WindowEnd = dto.WindowEnd?.ToUniversalTime(),
            Notes = TrimOrNull(dto.Notes),
            OccurredAt = occurredAt?.ToUniversalTime(),
            UnknownOptionalFields = unknown
        });
    }

    /// <summary>
    /// Maps a dispatch command to a FieldFlow create body.
    /// </summary>
    public FieldFlowCreateWorkOrderRequestDto ToCreateRequest(DispatchWorkOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new FieldFlowCreateWorkOrderRequestDto
        {
            ClientReference = command.ClientReference,
            ContractorId = command.ExternalContractorId,
            CustomerName = command.CustomerName,
            CustomerPhone = command.CustomerPhone,
            CustomerEmail = command.CustomerEmail,
            AddressStreet = command.AddressStreet,
            AddressUnit = command.AddressUnit,
            AddressCity = command.AddressCity,
            AddressPostal = command.AddressPostal,
            ServiceType = command.ServiceType,
            Subcategory = command.Subcategory,
            WindowStart = command.WindowStart,
            WindowEnd = command.WindowEnd,
            Notes = command.Notes
        };
    }

    private static IReadOnlyList<string> CollectUnknown(Dictionary<string, JsonElement>? extensionData) =>
        extensionData is null || extensionData.Count == 0
            ? []
            : extensionData.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToArray();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
