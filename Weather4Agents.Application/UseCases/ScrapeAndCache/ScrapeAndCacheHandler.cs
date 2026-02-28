using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.ScrapeAndCache;

public class ScrapeAndCacheHandler : ICommandHandler<ScrapeAndCacheCommand>
{
    private readonly IWeatherProviderResolver _resolver;

    public ScrapeAndCacheHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task HandleAsync(ScrapeAndCacheCommand command, CancellationToken ct)
    {
        var scraper = _resolver.GetByName(command.ProviderName);
        await scraper.GetForecastAsync(command.Location, forceRefresh: true, ct);
    }
}
