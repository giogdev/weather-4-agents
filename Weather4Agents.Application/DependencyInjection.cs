using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.ScrapeAndCache;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        services.AddScoped<IQueryHandler<GetWeatherForecastQuery, IEnumerable<DayWeather>>, GetWeatherForecastHandler>();
        services.AddScoped<IQueryHandler<GetDayWeatherQuery, DayWeather?>, GetDayWeatherHandler>();
        services.AddScoped<ICommandHandler<ScrapeAndCacheCommand>, ScrapeAndCacheHandler>();

        return services;
    }
}
