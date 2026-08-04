using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetDayWeather;

public class GetDayWeatherHandler : IQueryHandler<GetDayWeatherQuery, DayWeatherResponse?>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetDayWeatherHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<DayWeatherResponse?> HandleAsync(GetDayWeatherQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var scraped = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        // A non-empty forecast that simply lacks the requested date maps to null; the controller
        // turns that into a 404 for the specific day.
        return DayWeatherResponse.From(scraped, query.Date);
    }
}
