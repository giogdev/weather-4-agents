using System;
using System.Collections.Generic;
using System.Text;
using Weather4Agents.Domain.Enums;

namespace Weather4Agents.Domain.Entities
{
    /// <summary>
    /// Detailed weather information from a specific time range of the day
    /// </summary>
    public class HoursWeatherDetails
    {
        /// <summary>
        /// Hour from which the forecast is valid
        /// </summary>
        public TimeOnly TimeFrom { get; set; }
        /// <summary>
        /// Hour to which the forecast is valid
        /// </summary>
        public TimeOnly TimeTo { get; set; }
        /// <summary>
        /// Type of weather (e.g., sunny, cloudy, rainy)
        /// </summary>
        public string WeatherType { get; set; } = Weather4Agents.Domain.Enums.WeatherType.Unknown;
        /// <summary>
        /// Weather description from provider website
        /// </summary>
        public string WeatherTypeDescription { get; set; } = "";
        /// <summary>
        /// Temperature
        /// </summary>
        public double TemperatureC { get; set; }
        /// <summary>
        /// Precipitation in mm
        /// </summary>
        public double PrecipitationMm { get; set; }
        /// <summary>
        /// Humidity
        /// </summary>
        public int HumidityPerc { get; set; }
        /// <summary>
        /// Athmospheric pressure in mbar
        /// </summary>
        public int PressionMbar { get; set; }
        /// <summary>
        /// Wind speed in km/h
        /// </summary>
        public double WindKmh { get; set; }
        /// <summary>
        /// Wind direction (e.g., N, NE, E, SE, S, SW, W, NW)
        /// </summary>
        public string WindDirection { get; set; } = "";
        /// <summary>
        /// Precipitation probability for this time slot (0-100), if provided by the source.
        /// Null when the provider does not expose per-slot precipitation probability.
        /// </summary>
        public int? PrecipitationProbabilityPerc { get; set; }
    }
}
