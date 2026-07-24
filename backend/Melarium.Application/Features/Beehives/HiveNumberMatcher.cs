using System.Text.RegularExpressions;

namespace Melarium.Application.Features.Beehives;

/// <summary>
/// Resolves a scanned/typed number to beehives. The physical <see cref="Domain.Entities.Beehive.LabelNumber"/>
/// is the source of truth; a hive without one falls back to a number parsed from its
/// <see cref="Domain.Entities.Beehive.Name"/>. Comparison uses a canonical form (trimmed, upper-cased,
/// pure numbers stripped of leading zeros) so "1", "01" and "Košnica 001" all match a scanned "1".
/// </summary>
public static class HiveNumberMatcher
{
    private static readonly Regex DigitGroups = new(@"\d+", RegexOptions.Compiled);

    /// <summary>Canonical comparison form, or null when there is no usable token.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().ToUpperInvariant();

        // Pure number → drop leading zeros ("007" → "7", "000" → "0").
        if (s.All(char.IsAsciiDigit))
        {
            var trimmed = s.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }
        return s;
    }

    /// <summary>Canonical number tokens inside a free-text name ("Grupa 2 - košnica 1" → ["2", "1"]).</summary>
    public static IReadOnlyList<string> NameNumberTokens(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return [];
        return DigitGroups.Matches(name).Select(m => Normalize(m.Value)!).ToList();
    }

    /// <summary>
    /// Best single number to seed a <c>LabelNumber</c> from a name — the last group,
    /// since the hive number usually comes last ("2. red, košnica 5" → "5").
    /// </summary>
    public static string? PrimaryNameNumber(string? name)
    {
        var tokens = NameNumberTokens(name);
        return tokens.Count > 0 ? tokens[^1] : null;
    }

    /// <summary>
    /// True when <paramref name="canonicalTarget"/> (already <see cref="Normalize"/>d) resolves to this hive.
    /// A set <paramref name="labelNumber"/> is authoritative — no name fallback in that case.
    /// </summary>
    public static bool Matches(string? labelNumber, string? name, string canonicalTarget)
    {
        var label = Normalize(labelNumber);
        if (label is not null)
            return label == canonicalTarget;

        return NameNumberTokens(name).Contains(canonicalTarget);
    }
}
