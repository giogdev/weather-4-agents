using MediatR;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetDayWeather;

public class GetDayWeatherHandler : IRequestHandler<GetDayWeatherQuery, DayWeather?>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetDayWeatherHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<DayWeather?> Handle(GetDayWeatherQuery request, CancellationToken ct)
    {
        var scraper = request.ProviderName is not null
            ? _resolver.GetByName(request.ProviderName)
            : _resolver.GetDefault();

        var forecast = await scraper.GetForecastAsync(request.Location, forceRefresh: false, ct);

        return forecast.FirstOrDefault(d => d.Date == request.Date);
    }
}
