using Microsoft.Extensions.DependencyInjection;
using Weather4Agents.Application.UseCases.GetWeatherForecast;

namespace Weather4Agents.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetWeatherForecastHandler).Assembly));

        return services;
    }
}
