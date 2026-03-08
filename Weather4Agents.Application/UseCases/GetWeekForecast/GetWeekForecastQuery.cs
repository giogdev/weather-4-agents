using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;

namespace Weather4Agents.Application.UseCases.GetWeekForecast;

public record GetWeekForecastQuery(string Location, string? ProviderName) : IQuery<WeekForecastResponse>;
