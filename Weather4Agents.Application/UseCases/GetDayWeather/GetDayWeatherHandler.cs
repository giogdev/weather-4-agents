using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetDayWeather;

public class GetDayWeatherHandler : IQueryHandler<GetDayWeatherQuery, DayWeather?>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetDayWeatherHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<DayWeather?> HandleAsync(GetDayWeatherQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var scraped = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        // A non-empty forecast that simply lacks the requested date returns null; the controller
        // maps that to a 404 for the specific day.
        return scraped.Days.FirstOrDefault(d => d.Date == query.Date);
    }
}
