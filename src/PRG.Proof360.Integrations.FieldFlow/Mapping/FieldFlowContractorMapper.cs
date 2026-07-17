using System.Text.Json;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow.Mapping;

/// <summary>
/// Converts FieldFlow contractor DTOs into provider-neutral snapshots.
/// </summary>
public sealed class FieldFlowContractorMapper
{
    /// <summary>
    /// Maps a validated contractor DTO to a snapshot.
    /// </summary>
    public Result<ContractorSnapshot, ProviderFailure> ToSnapshot(
        FieldFlowContractorDto dto,
        string providerInstanceId,
        string? schemaVersion = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ContractorId))
        {
            return Result<ContractorSnapshot, ProviderFailure>.Fail(
                ProviderFailure.Validation("missing_contractor_id", "contractorId is required."));
        }

        if (!FieldFlowDateParser.TryParseDate(dto.License?.ExpiresOn, out var licenseExpiry, out var licenseError))
        {
            return Result<ContractorSnapshot, ProviderFailure>.Fail(licenseError!);
        }

        if (!FieldFlowDateParser.TryParseDate(dto.Insurance?.ExpiresOn, out var insuranceExpiry, out var insuranceError))
        {
            return Result<ContractorSnapshot, ProviderFailure>.Fail(insuranceError!);
        }

        var unknown = CollectUnknown(dto.ExtensionData);

        return Result<ContractorSnapshot, ProviderFailure>.Ok(new ContractorSnapshot
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = providerInstanceId,
            ExternalContractorId = dto.ContractorId.Trim(),
            ComplianceId = TrimOrNull(dto.ComplianceId),
            IsActive = dto.Active,
            LicenseNumber = TrimOrNull(dto.License?.Number),
            LicenseExpiry = licenseExpiry,
            InsurancePolicy = TrimOrNull(dto.Insurance?.Policy),
            InsuranceExpiry = insuranceExpiry,
            InsuranceCoverage = TrimOrNull(dto.Insurance?.Coverage),
            WcbNumber = TrimOrNull(dto.WcbNumber),
            SchemaVersion = schemaVersion,
            UnknownOptionalFields = unknown
        });
    }

    private static IReadOnlyList<string> CollectUnknown(Dictionary<string, JsonElement>? extensionData) =>
        extensionData is null || extensionData.Count == 0
            ? []
            : extensionData.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToArray();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
