using Weather4Agents.Application.CQRS;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetWeatherForecast;

/// <param name="Location">City or location name.</param>
/// <param name="ProviderName">Optional provider name. If null, the default provider is used.</param>
/// <param name="Days">Optional number of days to return. If null, all available days are returned.</param>
public record GetWeatherForecastQuery(string Location, string? ProviderName, int? Days = null)
    : IQuery<IEnumerable<DayWeather>>;
