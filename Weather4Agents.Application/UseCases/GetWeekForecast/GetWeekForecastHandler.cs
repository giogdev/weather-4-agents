using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetWeekForecast;

public class GetWeekForecastHandler : IQueryHandler<GetWeekForecastQuery, WeekForecastResponse>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetWeekForecastHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<WeekForecastResponse> HandleAsync(GetWeekForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var allDays = await scraper.GetForecastAsync(query.Location, forceRefresh: false, ct);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var forecast = allDays
            .Where(d => d.Date > today)
            .OrderBy(d => d.Date)
            .Take(7)
            .Select(d => new DayForecastEntry
            {
                Date = d.Date,
                HoursDetails = d.HoursDetails
            });

        return new WeekForecastResponse
        {
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Forecast = forecast
        };
    }
}
