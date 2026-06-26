using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.Application.UseCases.GetNext24HoursForecast;

public class GetNext24HoursForecastHandler : IQueryHandler<GetNext24HoursForecastQuery, Next24HoursForecastResponse>
{
    private readonly IWeatherProviderResolver _resolver;

    public GetNext24HoursForecastHandler(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<Next24HoursForecastResponse> HandleAsync(GetNext24HoursForecastQuery query, CancellationToken ct)
    {
        var scraper = query.ProviderName is not null
            ? _resolver.GetByName(query.ProviderName)
            : _resolver.GetDefault();

        var allDays = await scraper.GetForecastAsync(query.Location, forceRefresh: false, ct);

        var now = DateTime.Now;
        var windowEnd = now.AddHours(24);

        var hours = allDays
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
            LastUpdatedAt = DateTimeOffset.UtcNow,
            Hours = hours
        };
    }
}
