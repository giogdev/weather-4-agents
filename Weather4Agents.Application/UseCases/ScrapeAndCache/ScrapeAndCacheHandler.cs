using MediatR;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.ScrapeAndCache;

public class ScrapeAndCacheHandler : IRequestHandler<ScrapeAndCacheCommand>
{
    private readonly IWeatherProviderResolver _resolver;

    public ScrapeAndCacheHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task Handle(ScrapeAndCacheCommand request, CancellationToken ct)
    {
        var scraper = _resolver.GetByName(request.ProviderName);
        await scraper.GetForecastAsync(request.Location, forceRefresh: true, ct);
    }
}
