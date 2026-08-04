using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Weather4Agents.Application.Settings;
using Weather4Agents.Domain.ValueObjects;

namespace Weather4Agents.API.Filters;

/// <summary>
/// Enforces the opt-in location whitelist (<see cref="WeatherScrapingSettings.AllowUnconfiguredLocations"/>).
/// Runs before the controller action — hence before any scrape — so a request for a
/// non-configured location is rejected with a <c>403</c> ProblemDetails without touching the
/// provider. When the whitelist is disabled (the default) the filter is a no-op. Matching uses
/// the normalized spelling, so spaced and hyphenated forms of the same place both match.
/// </summary>
public sealed class ServableLocationFilter : IAsyncActionFilter
{
    private const string LocationArgumentName = "location";

    private readonly bool _allowUnconfiguredLocations;
    private readonly HashSet<string> _allowedLocations;

    public ServableLocationFilter(IOptions<WeatherScrapingSettings> options)
    {
        var settings = options.Value;
        _allowUnconfiguredLocations = settings.AllowUnconfiguredLocations;
        _allowedLocations = settings.Locations
            .Select(LocationName.Normalize)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!_allowUnconfiguredLocations
            && context.ActionArguments.TryGetValue(LocationArgumentName, out var raw)
            && raw is string location
            && !_allowedLocations.Contains(LocationName.Normalize(location)))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Location not allowed",
                Detail = $"Location '{location}' is not in the configured whitelist. "
                         + "This instance only serves the locations it is configured for."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
