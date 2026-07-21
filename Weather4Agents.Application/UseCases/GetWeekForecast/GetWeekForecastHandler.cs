using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetWeekForecast;

public class GetWeekForecastHandler : IQueryHandler<GetWeekForecastQuery, WeekForecastResponse>
{
    private readonly IWeatherProviderResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public GetWeekForecastHandler(IWeatherProviderResolver resolver, TimeProvider timeProvider)
    {
        _resolver = resolver;
        _timeProvider = timeProvider;
    }

    public async Task<WeekForecastResponse> HandleAsync(GetWeekForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var allDays = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        // "Today" in the provider's timezone: around midnight the host-timezone date (e.g. a UTC
        // container) lags the provider's and would resurrect an already-past day.
        var today = scraper.GetLocalToday(_timeProvider);
        var forecast = allDays
            .Where(d => d.Date >= today)
            .OrderBy(d => d.Date)
            .Take(7)
            .Select(d => new DayForecastEntry
            {
                Date = d.Date,
                ReliabilityPerc = d.ReliabilityPerc,
                HoursDetails = d.HoursDetails
            });

        return new WeekForecastResponse
        {
            LastUpdatedAt = _timeProvider.GetUtcNow(),
            Timezone = scraper.TimeZone.Id,
            Forecast = forecast
        };
    }
}
