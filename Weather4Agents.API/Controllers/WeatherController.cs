using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Weather4Agents.API.Filters;
using Weather4Agents.API.Settings;
using Weather4Agents.API.Validation;
using Weather4Agents.Application.CQRS;
using Weather4Agents.Application.DTOs;
using Weather4Agents.Application.UseCases.GetDayWeather;
using Weather4Agents.Application.UseCases.GetNext24HoursForecast;
using Weather4Agents.Application.UseCases.GetTodayWeather;
using Weather4Agents.Application.UseCases.GetWeatherForecast;
using Weather4Agents.Application.UseCases.GetWeekForecast;
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
    [ProducesResponseType<ForecastResponse>(StatusCodes.Status200OK)]
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
        return CacheableOk(ForecastResponse.From(result));
    }

    /// <summary>
    /// 7-day weather forecast
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/week")]
    [ProducesResponseType<WeekForecastResponse>(StatusCodes.Status200OK)]
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
        return CacheableOk(result);
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
        return CacheableOk(result);
    }

    /// <summary>
    /// Weather for a specific day
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="date">Date for which to retrieve weather information.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/date/{date}")]
    [ProducesResponseType<DayWeatherResponse>(StatusCodes.Status200OK)]
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
            : CacheableOk(result);
    }

    /// <summary>
    /// Weather for the current day, so callers need not compute today's date. "Today" is taken in
    /// the provider's timezone; the payload is identical to requesting today's date explicitly.
    /// </summary>
    /// <param name="location">Location name: letters, spaces, apostrophes and hyphens only. If location contains spaces, use URL encoding.</param>
    /// <param name="provider">Optional provider name. If omitted, the default provider is used.</param>
    /// <param name="ct"></param>
    [HttpGet("{location}/forecast/today")]
    [ProducesResponseType<DayWeatherResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetTodayWeather(
        [ValidLocation] string location,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new GetTodayWeatherQuery(location, provider), ct);
        return result is null
            ? Problem(detail: $"No weather data found for '{location}' today.", statusCode: StatusCodes.Status404NotFound)
            : CacheableOk(result);
    }

    /// <summary>
    /// Serves a forecast response with an <c>ETag</c> derived from the data's scrape timestamp and a
    /// revalidation <c>Cache-Control</c>, and short-circuits to <c>304 Not Modified</c> when the
    /// caller's <c>If-None-Match</c> already matches — so polling agents stop re-downloading
    /// unchanged forecasts. The ETag rotates only when the underlying data is re-scraped.
    /// </summary>
    private IActionResult CacheableOk(IFreshnessStamped payload)
    {
        // Strong validator derived purely from the scrape time: it changes iff the data is
        // re-scraped. Conditional requests are scoped to a single URL by clients, so the same
        // timestamp on different endpoints never causes a false 304.
        var etag = new EntityTagHeaderValue(
            $"\"{payload.LastUpdatedAt.UtcTicks.ToString("x", CultureInfo.InvariantCulture)}\"");

        var responseHeaders = Response.GetTypedHeaders();
        responseHeaders.ETag = etag;
        // Store-but-revalidate: caches may keep the body but must revalidate with this API before
        // reusing it, which is what turns a repeat poll into a cheap 304.
        responseHeaders.CacheControl = new CacheControlHeaderValue { Private = true, NoCache = true };

        // If-None-Match uses weak comparison (RFC 9110 §13.1.2): match on the opaque tag, and treat
        // "*" as matching any current representation.
        var ifNoneMatch = Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(t => t.Tag == "*" || t.Tag == etag.Tag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(payload);
    }
}
