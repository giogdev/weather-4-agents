using Microsoft.AspNetCore.Mvc;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.GetWeekForecast;

namespace Weather4Agents.API.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WeatherController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Retrieves the weather forecast using the default provider.</summary>
    [HttpGet("{location}")]
    public async Task<IActionResult> GetForecast(string location, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetWeatherForecastQuery(location, null), ct);
        return Ok(result);
    }

    /// <summary>Retrieves the weather forecast using the specified provider.</summary>
    [HttpGet("{location}/{provider}")]
    public async Task<IActionResult> GetForecastByProvider(string location, string provider, CancellationToken ct)
    {
        try
        {
            var result = await _dispatcher.SendAsync(new GetWeatherForecastQuery(location, provider), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>Retrieves the 7-day weather forecast using the default provider.</summary>
    [HttpGet("{location}/forecast/week")]
    public async Task<IActionResult> GetWeekForecast(string location, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetWeekForecastQuery(location, null), ct);
        return Ok(result);
    }

    /// <summary>Retrieves the 7-day weather forecast using the specified provider.</summary>
    [HttpGet("{location}/{provider}/forecast/week")]
    public async Task<IActionResult> GetWeekForecastByProvider(string location, string provider, CancellationToken ct)
    {
        try
        {
            var result = await _dispatcher.SendAsync(new GetWeekForecastQuery(location, provider), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>Retrieves the weather for a specific day using the default provider.</summary>
    [HttpGet("{location}/day/{date}")]
    public async Task<IActionResult> GetDayWeather(string location, DateOnly date, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetDayWeatherQuery(location, date, null), ct);
        return result is null
            ? Problem(detail: $"No weather data found for '{location}' on {date:yyyy-MM-dd}.", statusCode: StatusCodes.Status404NotFound)
            : Ok(result);
    }

    /// <summary>Retrieves the weather for a specific day using the specified provider.</summary>
    [HttpGet("{location}/{provider}/day/{date}")]
    public async Task<IActionResult> GetDayWeatherByProvider(string location, string provider, DateOnly date, CancellationToken ct)
    {
        try
        {
            var result = await _dispatcher.SendAsync(new GetDayWeatherQuery(location, date, provider), ct);
            return result is null
                ? Problem(detail: $"No weather data found for '{location}' on {date:yyyy-MM-dd}.", statusCode: StatusCodes.Status404NotFound)
                : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }
}
