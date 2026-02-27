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

        var forecast = await scraper.GetForecastAsync(query.Location, forceRefresh: false, ct);

        return forecast.FirstOrDefault(d => d.Date == query.Date);
    }
}
