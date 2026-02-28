using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetWeatherForecast;

public class GetWeatherForecastHandler : IQueryHandler<GetWeatherForecastQuery, IEnumerable<DayWeather>>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetWeatherForecastHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<IEnumerable<DayWeather>> HandleAsync(GetWeatherForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        return await scraper.GetForecastAsync(query.Location, forceRefresh: false, ct);
    }
}
