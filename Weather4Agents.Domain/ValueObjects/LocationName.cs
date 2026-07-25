using System.Text.RegularExpressions;

namespace Weather4Agents.Domain.ValueObjects;

/// <summary>
/// Rules for location names. <see cref="IsValid"/> is the permissive shape check applied at the
/// API boundary; <see cref="Normalize"/> produces the single canonical spelling used for both
/// the cache key and the provider URL, so different spellings of the same place ("San
/// Pellegrino Terme", "san-pellegrino-terme") converge on one cache entry and one scrape.
/// </summary>
public static partial class LocationName
{
    public const int MaxLength = 100;

    /// <summary>
    /// Letters (any script, including combining accents), spaces, apostrophes and hyphens,
    /// with at least one letter and at most <see cref="MaxLength"/> characters.
    /// </summary>
    public static bool IsValid(string location)
        => location.Length <= MaxLength && ValidShapeRegex().IsMatch(location);

    /// <summary>
    /// Canonical spelling: trimmed, lowercase, with every run of whitespace and/or hyphens
    /// collapsed into a single hyphen.
    /// </summary>
    public static string Normalize(string location)
        => SeparatorRunRegex().Replace(location.Trim(), "-").ToLowerInvariant();

    [GeneratedRegex(@"^(?=.*\p{L})[\p{L}\p{M}' \-]+$")]
    private static partial Regex ValidShapeRegex();

    [GeneratedRegex(@"[\s\-]+")]
    private static partial Regex SeparatorRunRegex();
}
