using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;

namespace Weather4Agents.Application.UseCases.GetTodayWeather;

/// <param name="Location">City or location name.</param>
/// <param name="ProviderName">Optional provider name. If null, the default provider is used.</param>
public record GetTodayWeatherQuery(string Location, string? ProviderName)
    : IQuery<DayWeatherResponse?>;
