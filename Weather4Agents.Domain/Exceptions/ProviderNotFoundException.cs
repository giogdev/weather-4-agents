namespace Weather4Agents.Domain.Exceptions;

/// <summary>
/// Thrown when a caller requests a weather provider that is not registered.
/// Carries the available providers so callers can be told how to recover.
/// </summary>
public sealed class ProviderNotFoundException : Exception
{
    public string RequestedProvider { get; }

    public IReadOnlyCollection<string> AvailableProviders { get; }

    public ProviderNotFoundException(string requestedProvider, IReadOnlyCollection<string> availableProviders)
        : base($"Provider '{requestedProvider}' not found. Available providers: {string.Join(", ", availableProviders)}.")
    {
        RequestedProvider = requestedProvider;
        AvailableProviders = availableProviders;
    }
}
