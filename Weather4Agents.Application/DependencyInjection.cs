using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetNext24HoursForecast;
using Weather4Agents.Application.UseCases.GetTodayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.GetWeekForecast;
using Weather4Agents.Application.UseCases.ScrapeAndCache;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        services.AddScoped<IQueryHandler<GetWeatherForecastQuery, ScrapedForecast>, GetWeatherForecastHandler>();
        services.AddScoped<IQueryHandler<GetDayWeatherQuery, DayWeatherResponse?>, GetDayWeatherHandler>();
        services.AddScoped<IQueryHandler<GetTodayWeatherQuery, DayWeatherResponse?>, GetTodayWeatherHandler>();
        services.AddScoped<IQueryHandler<GetWeekForecastQuery, WeekForecastResponse>, GetWeekForecastHandler>();
        services.AddScoped<IQueryHandler<GetNext24HoursForecastQuery, Next24HoursForecastResponse>, GetNext24HoursForecastHandler>();
        services.AddScoped<ICommandHandler<ScrapeAndCacheCommand>, ScrapeAndCacheHandler>();

        return services;
    }
}
