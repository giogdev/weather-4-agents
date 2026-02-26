using MediatR;

namespace Weather4Agents.Application.UseCases.ScrapeAndCache;

/// <param name="Location">City or location name to scrape.</param>
/// <param name="ProviderName">Provider to use for scraping.</param>
public record ScrapeAndCacheCommand(string Location, string ProviderName) : IRequest;
