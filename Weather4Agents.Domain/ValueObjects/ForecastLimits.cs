namespace Weather4Agents.Domain.ValueObjects;

/// <summary>
/// Bounds shared by the API contract and the scrapers.
/// </summary>
public static class ForecastLimits
{
    /// <summary>
    /// Maximum number of forecast days a request may ask for: providers publish today plus
    /// the next seven days.
    /// </summary>
    public const int MaxDays = 8;
}
