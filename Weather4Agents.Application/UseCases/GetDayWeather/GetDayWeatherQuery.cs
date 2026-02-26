using MediatR;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application.UseCases.GetDayWeather;

/// <param name="Location">City or location name.</param>
/// <param name="Date">The specific day to retrieve.</param>
/// <param name="ProviderName">Optional provider name. If null, the default provider is used.</param>
public record GetDayWeatherQuery(string Location, DateOnly Date, string? ProviderName)
    : IRequest<DayWeather?>;
