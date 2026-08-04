namespace Weather4Agents.Domain.Exceptions;

/// <summary>
/// Thrown when a provider has no weather data for the requested location.
/// The code path that raises this for empty forecasts is delivered in ticket 07;
/// the global exception handler already maps it to <c>404 Not Found</c>.
/// </summary>
public sealed class LocationNotFoundException : Exception
{
    public string Location { get; }

    public string ProviderName { get; }

    public LocationNotFoundException(string location, string providerName)
        : base($"No weather data found for location '{location}' from provider '{providerName}'.")
    {
        Location = location;
        ProviderName = providerName;
    }
}
