using MediatR;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetWeatherForecast;

public class GetWeatherForecastHandler : IRequestHandler<GetWeatherForecastQuery, IEnumerable<DayWeather>>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetWeatherForecastHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<IEnumerable<DayWeather>> Handle(GetWeatherForecastQuery request, CancellationToken ct)
    {
        var scraper = request.ProviderName is not null
            ? _resolver.GetByName(request.ProviderName)
            : _resolver.GetDefault();

        return await scraper.GetForecastAsync(request.Location, forceRefresh: false, ct);
    }
}
