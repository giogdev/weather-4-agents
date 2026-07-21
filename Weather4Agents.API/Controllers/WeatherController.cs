using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Weather4Agents.API.Filters;
using Weather4Agents.API.Settings;
using Weather4Agents.API.Validation;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetNext24HoursForecast;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.GetWeekForecast;
using Weather4Agents.Domain.Entities;
using Weather4Agents.Domain.ValueObjects;

namespace Weather4Agents.API.Controllers;

/// <summary>
/// Weather-related API
/// </summary>
[ApiController]
[Route("api/weather")]
[EnableRateLimiting(RateLimitingSettings.PolicyName)]
[ServiceFilter(typeof(ServableLocationFilter))]
public class WeatherController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WeatherController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Forecast for the next <paramref name="numberOfDays"/> days
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="numberOfDays">Number of days to return, between 1 and 8.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/days/{numberOfDays}")]
    [ProducesResponseType<IEnumerable<DayWeather>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetForecastByDays(
        [ValidLocation] string location,
        [Range(1, ForecastLimits.MaxDays)] int numberOfDays,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetWeatherForecastQuery(location, provider, numberOfDays), ct);
        return Ok(result.Days);
    }

    /// <summary>
    /// 7-day weather forecast
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/week")]
    [ProducesResponseType<IEnumerable<DayWeather>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetWeekForecast(
        [ValidLocation] string location,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetWeekForecastQuery(location, provider), ct);
        return Ok(result);
    }

    /// <summary>
    /// Forecast for the next 24 hours
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/next-24h")]
    [ProducesResponseType<Next24HoursForecastResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetNext24HoursForecast(
        [ValidLocation] string location,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetNext24HoursForecastQuery(location, provider), ct);
        return Ok(result);
    }

    /// <summary>
    /// Weather for a specific day
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="date">Date for which to retrieve weather information.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/date/{date}")]
    [ProducesResponseType<DayWeather>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetDayWeather(
        [ValidLocation] string location,
        DateOnly date,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetDayWeatherQuery(location, date, provider), ct);
        return result is null
            ? Problem(detail: $"No weather data found for '{location}' on {date:yyyy-MM-dd}.", statusCode: StatusCodes.Status404NotFound)
            : Ok(result);
    }
}
