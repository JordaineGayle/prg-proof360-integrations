using System.Globalization;
using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow.Mapping;

/// <summary>
/// Parses FieldFlow date strings into <see cref="DateOnly"/> with stable validation failures.
/// </summary>
internal static class FieldFlowDateParser
{
    /// <summary>
    /// Parses <c>yyyy-MM-dd</c> (or a full ISO datetime, taking the UTC calendar date).
    /// </summary>
    public static bool TryParseDate(string? value, out DateOnly? date, out ProviderFailure? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (DateOnly.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            date = dateOnly;
            return true;
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            date = DateOnly.FromDateTime(dto.UtcDateTime);
            return true;
        }

        error = ProviderFailure.Validation(
            "invalid_date",
            "Date value could not be parsed as yyyy-MM-dd or ISO-8601 UTC.");
        return false;
    }
}
