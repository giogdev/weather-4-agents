using MediatR;
using Microsoft.AspNetCore.Mvc;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;

namespace Weather4Agents.API.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly IMediator _mediator;

    public WeatherController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Retrieves the weather forecast using the default provider.</summary>
    [HttpGet("{location}")]
    public async Task<IActionResult> GetForecast(string location, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWeatherForecastQuery(location, null), ct);
        return Ok(result);
    }

    /// <summary>Retrieves the weather forecast using the specified provider.</summary>
    [HttpGet("{location}/{provider}")]
    public async Task<IActionResult> GetForecastByProvider(string location, string provider, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetWeatherForecastQuery(location, provider), ct);
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
        var result = await _mediator.Send(new GetDayWeatherQuery(location, date, null), ct);
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
            var result = await _mediator.Send(new GetDayWeatherQuery(location, date, provider), ct);
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
