using System.Text.Json;
using PRG.Proof360.Integrations.Application.Errors;

namespace PRG.Proof360.Integrations.Application.Observability;

/// <summary>One sanitized failure entry retained on inbox/outbox rows.</summary>
public sealed record FailureHistoryEntry(
    DateTimeOffset At,
    string Category,
    string Code,
    string SafeMessage,
    int Attempt,
    string? CausationId);

/// <summary>
/// Append-only failure history helpers. Never stores secrets or raw payloads.
/// </summary>
public static class FailureHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Appends a sanitized failure entry to existing JSON history.</summary>
    public static string Append(
        string? existingJson,
        IntegrationFailure failure,
        int attempt,
        DateTimeOffset at,
        string? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var entries = DeserializeMutable(existingJson);
        entries.Add(new FailureHistoryEntry(
            at,
            failure.Category.ToString(),
            failure.Code,
            Truncate(failure.SafeMessage, 200),
            attempt,
            causationId));

        // Bound growth for the prototype.
        if (entries.Count > 50)
        {
            entries = entries.Skip(entries.Count - 50).ToList();
        }

        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    /// <summary>Deserializes history (empty on parse failure).</summary>
    public static IReadOnlyList<FailureHistoryEntry> Deserialize(string? json) => DeserializeMutable(json);

    private static List<FailureHistoryEntry> DeserializeMutable(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<FailureHistoryEntry>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
