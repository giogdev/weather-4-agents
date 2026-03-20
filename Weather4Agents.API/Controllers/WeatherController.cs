using Microsoft.AspNetCore.Mvc;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.GetWeekForecast;
using Weather4Agents.Domain.Entities;

namespace Weather4Agents.API.Controllers;

/// <summary>
/// Weather-related API
/// </summary>
[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WeatherController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Retrieves the weather forecast for the next <paramref name="numberOfDays"/> days using the specified provider (or the default one if omitted).
    /// </summary>
    /// <param name="location">Location name. If location contains spaces, use URL encoding.</param>
    /// <param name="numberOfDays">Number of days to return</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/days/{numberOfDays}")]
    [ProducesResponseType<IEnumerable<DayWeather>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForecastByDays(string location, int numberOfDays, [FromQuery] string? provider, CancellationToken ct)
    {
        try
        {
            var result = await _dispatcher.SendAsync(new GetWeatherForecastQuery(location, provider, numberOfDays), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Retrieves the 7-day weather forecast using the specified provider (or the default one if omitted).
    /// </summary>
    /// <param name="location">Location name. If location contains spaces, use URL encoding.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/week")]
    [ProducesResponseType<IEnumerable<DayWeather>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeekForecast(string location, [FromQuery] string? provider, CancellationToken ct)
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

    /// <summary>
    /// Retrieves the weather for a specific day using the specified provider (or the default one if omitted).
    /// </summary>
    /// <param name="location">Location name. If location contains spaces, use URL encoding.</param>
    /// <param name="date">Date for which to retrieve weather information.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/date/{date}")]
    [ProducesResponseType<DayWeather>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDayWeather(string location, DateOnly date, [FromQuery] string? provider, CancellationToken ct)
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
