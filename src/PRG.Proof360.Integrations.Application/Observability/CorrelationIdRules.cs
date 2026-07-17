using System.Text.RegularExpressions;

namespace PRG.Proof360.Integrations.Application.Observability;

/// <summary>
/// Validates caller-supplied correlation IDs. Invalid values are replaced, never trusted as-is.
/// </summary>
public static partial class CorrelationIdRules
{
    /// <summary>Maximum accepted correlation id length.</summary>
    public const int MaxLength = 128;

    /// <summary>Allowed characters: alphanumeric, dash, underscore, colon, period.</summary>
    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPattern();

    /// <summary>
    /// Returns a safe correlation id: trims valid header values, otherwise generates a new id.
    /// </summary>
    public static string Resolve(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return NewId();
        }

        var trimmed = candidate.Trim();
        if (trimmed.Length > MaxLength || !AllowedPattern().IsMatch(trimmed))
        {
            return NewId();
        }

        return trimmed;
    }

    /// <summary>Creates a new correlation/causation identifier.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>True when the candidate is acceptable as-is (after trim).</summary>
    public static bool IsValid(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim();
        return trimmed.Length <= MaxLength && AllowedPattern().IsMatch(trimmed);
    }
}
