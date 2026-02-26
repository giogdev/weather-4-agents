using MediatR;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetWeatherForecast;

/// <param name="Location">City or location name.</param>
/// <param name="ProviderName">Optional provider name. If null, the default provider is used.</param>
public record GetWeatherForecastQuery(string Location, string? ProviderName)
    : IRequest<IEnumerable<DayWeather>>;
