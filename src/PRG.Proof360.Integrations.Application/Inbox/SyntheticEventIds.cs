using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.Application.Inbox;

/// <summary>
/// Deterministic synthetic event identities for polling snapshots.
/// Prefer provider entity version; fall back to payload hash when version is unavailable.
/// Random IDs are forbidden because they defeat inbox deduplication.
/// </summary>
public static class SyntheticEventIds
{
    /// <summary>
    /// Builds a contractor poll event id.
    /// Format: <c>poll:{instance}:contractor:{externalId}:v{version|h{hash}}</c>.
    /// </summary>
    public static string ForContractor(ContractorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var versionPart = snapshot.EntityVersion is { } version
            ? $"v{version}"
            : $"h{HashPayload(snapshot)}";
        return $"poll:{snapshot.ProviderInstanceId}:{ExternalEntityTypes.Contractor}:{snapshot.ExternalContractorId}:{versionPart}";
    }

    /// <summary>
    /// Builds a work-order poll event id.
    /// Format: <c>poll:{instance}:work_order:{externalId}:v{version}</c> (version required on work orders).
    /// </summary>
    public static string ForWorkOrder(WorkOrderSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return $"poll:{snapshot.ProviderInstanceId}:{ExternalEntityTypes.WorkOrder}:{snapshot.ExternalWorkOrderId}:v{snapshot.EntityVersion}";
    }

    /// <summary>SHA-256 hex of a stable JSON serialization of the snapshot.</summary>
    public static string HashPayload<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
