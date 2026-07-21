using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetNext24HoursForecast;

public class GetNext24HoursForecastHandler : IQueryHandler<GetNext24HoursForecastQuery, Next24HoursForecastResponse>
{
    private readonly IWeatherProviderResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public GetNext24HoursForecastHandler(IWeatherProviderResolver resolver, TimeProvider timeProvider)
    {
        _resolver = resolver;
        _timeProvider = timeProvider;
    }

    public async Task<Next24HoursForecastResponse> HandleAsync(GetNext24HoursForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var scraped = await scraper.GetForecastOrNotFoundAsync(query.Location, ct);

        // The provider's slot dates/times are local to its timezone, so the window bounds must be
        // too: a host-timezone "now" (e.g. a UTC container) would shift the window by hours.
        var now = scraper.GetLocalNow(_timeProvider);
        var windowEnd = now.AddHours(24);

        var hours = scraped.Days
            .SelectMany(d => d.HoursDetails.Select(h => new
            {
                d.Date,
                d.ReliabilityPerc,
                Details = h,
                Start = d.Date.ToDateTime(h.TimeFrom),
                // Slots crossing midnight have TimeTo earlier than TimeFrom; roll over to the next day.
                End = h.TimeTo >= h.TimeFrom
                    ? d.Date.ToDateTime(h.TimeTo)
                    : d.Date.AddDays(1).ToDateTime(h.TimeTo)
            }))
            // Keep slots that are still ongoing or upcoming and start within the next 24 hours.
            .Where(x => x.End > now && x.Start < windowEnd)
            .OrderBy(x => x.Start)
            .Select(x => new HourlyForecastEntry
            {
                Date = x.Date,
                ReliabilityPerc = x.ReliabilityPerc,
                Details = x.Details
            });

        return new Next24HoursForecastResponse
        {
            LastUpdatedAt = scraped.ScrapedAt,
            Timezone = scraper.TimeZone.Id,
            Hours = hours
        };
    }
}
