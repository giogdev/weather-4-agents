using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Interfaces.Scrapers;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetWeatherForecast;

public class GetWeatherForecastHandler : IQueryHandler<GetWeatherForecastQuery, ScrapedForecast>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetWeatherForecastHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<ScrapedForecast> HandleAsync(GetWeatherForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var scraped = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        if (!query.Days.HasValue)
            return scraped;

        // Trim into a new envelope rather than mutating the one returned from the cache: the
        // cached forecast must stay intact for other callers. The scrape time is preserved, so
        // freshness reflects the scrape, not how many days the caller asked for.
        return new ScrapedForecast
        {
            ScrapedAt = scraped.ScrapedAt,
            Days = scraped.Days.Take(query.Days.Value).ToList()
        };
    }
}
