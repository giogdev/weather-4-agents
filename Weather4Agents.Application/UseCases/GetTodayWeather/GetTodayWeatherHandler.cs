using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetTodayWeather;

/// <summary>
/// Resolves the current day's weather so the caller does not have to compute the date. "Today" is
/// taken in the provider's timezone (ticket 08): around midnight the host-timezone date (e.g. a UTC
/// container) can differ from the provider's and would return the wrong day. The mapping is
/// identical to the explicit date endpoint, so the payload matches <c>date/{today}</c> exactly.
/// </summary>
public class GetTodayWeatherHandler : IQueryHandler<GetTodayWeatherQuery, DayWeatherResponse?>
{
    private readonly IWeatherProviderResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public GetTodayWeatherHandler(IWeatherProviderResolver resolver, TimeProvider timeProvider)
    {
        _resolver = resolver;
        _timeProvider = timeProvider;
    }

    public async Task<DayWeatherResponse?> HandleAsync(GetTodayWeatherQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var scraped = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        var today = scraper.GetLocalToday(_timeProvider);

        // A non-empty forecast that simply lacks today maps to null; the controller turns that into
        // a 404 for the day, exactly as the explicit date endpoint does.
        return DayWeatherResponse.From(scraped, today);
    }
}
