using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weather4Agents.Domain.Enums;
using Weather4Agents.Infrastructure.Scrapers;

namespace Weather4Agents.Test.Scrapers;

public class Meteo3bWeatherTypeMapperTests
{
    private static Meteo3bWeatherTypeMapper CreateMapper() =>
        new(NullLogger<Meteo3bWeatherTypeMapper>.Instance);

    // -------------------------------------------------------------------------
    // Plural "piogge" phrases map to rain (was broken by the "pioggere" typo)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Piogge diffuse")]
    [InlineData("Nubi sparse con piogge")]
    public void Map_PluralPioggePhrases_MapsToRainy(string description)
    {
        var result = CreateMapper().Map(description);

        Assert.Equal(WeatherType.Rainy, result);
    }

    // -------------------------------------------------------------------------
    // "possibili piogge" combinations keep their priority over the generic
    // rain rule, which now also matches the plural "piogge"
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Nubi sparse con possibili piogge")]
    [InlineData("Poco nuvoloso con possibili piogge")]
    public void Map_PossibiliPioggeWithScatteredClouds_MapsToLightRain(string description)
    {
        var result = CreateMapper().Map(description);

        Assert.Equal(WeatherType.LightRain, result);
    }

    [Fact]
    public void Map_PossibiliPioggeAlone_MapsToProbablyRainy()
    {
        var result = CreateMapper().Map("Possibili piogge");

        Assert.Equal(WeatherType.ProbablyRainy, result);
    }

    [Fact]
    public void Map_PossibiliPioggeWithShowers_KeepsHeavyRainPriority()
    {
        // "possibili piogge" takes priority over the generic rain rule only:
        // heavier signals such as "acquazzone" still win, as they did before
        // the plural "piogge" fix.
        var result = CreateMapper().Map("Possibili piogge con acquazzone");

        Assert.Equal(WeatherType.HeavyRain, result);
    }

    // -------------------------------------------------------------------------
    // Drizzle maps to LightRain, not the generic rain rule
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("Pioviggine")]
    [InlineData("Pioggia debole")]
    public void Map_Drizzle_MapsToLightRain(string description)
    {
        var result = CreateMapper().Map(description);

        Assert.Equal(WeatherType.LightRain, result);
    }

    // -------------------------------------------------------------------------
    // Documented precedence decisions (intentional, not accidental)
    // -------------------------------------------------------------------------

    [Fact]
    public void Map_SerenoConVelature_MapsToSunny()
    {
        // Intended: "sereno" wins over "velature" — a clear sky with thin high
        // veils is still reported as Sunny. LightClouds is reserved for
        // descriptions where "velature" is the dominant condition.
        var result = CreateMapper().Map("Sereno con velature");

        Assert.Equal(WeatherType.Sunny, result);
    }

    [Fact]
    public void Map_VelatureAlone_MapsToLightClouds()
    {
        var result = CreateMapper().Map("Velature estese");

        Assert.Equal(WeatherType.LightClouds, result);
    }

    [Fact]
    public void Map_VentoForteAlone_MapsToHeavyWindy()
    {
        // Intended: HeavyWindy applies only when wind is the whole description;
        // in combinations (e.g. "nuvoloso con vento forte") the sky condition
        // wins because agents care about precipitation/sky first.
        var result = CreateMapper().Map("Vento forte");

        Assert.Equal(WeatherType.HeavyWindy, result);
    }

    [Fact]
    public void Map_NuvolosoConVentoForte_MapsToCloudy()
    {
        var result = CreateMapper().Map("Nuvoloso con vento forte");

        Assert.Equal(WeatherType.Cloudy, result);
    }

    // -------------------------------------------------------------------------
    // Unknown descriptions are logged as warnings with the original text
    // -------------------------------------------------------------------------

    [Fact]
    public void Map_UnknownDescription_ReturnsUnknownAndLogsWarningWithDescription()
    {
        var logger = new CapturingLogger();
        var mapper = new Meteo3bWeatherTypeMapper(logger);

        var result = mapper.Map("Tempesta di sabbia");

        Assert.Equal(WeatherType.Unknown, result);
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Tempesta di sabbia", warning.Message);
    }

    [Fact]
    public void Map_KnownDescription_LogsNoWarning()
    {
        var logger = new CapturingLogger();
        var mapper = new Meteo3bWeatherTypeMapper(logger);

        mapper.Map("Sereno");

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    private sealed class CapturingLogger : ILogger<Meteo3bWeatherTypeMapper>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
