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
        public TimeOnly TimeFrom { get; set; }
        public TimeOnly TimeTo { get; set; }
        public string WeatherType { get; set; } = Weather4Agents.Domain.Enums.WeatherType.Unknown;
        public string WeatherTypeDescription { get; set; } = "";
        public double TemperatureC { get; set; }
        public double PrecipitationMm { get; set; }
        public int HumidityPerc { get; set; }
        public int PressionMbar { get; set; }
        public double WindKmh { get; set; }
        public string WindDirection { get; set; } = "";
        /// <summary>
        /// Forecast reliability percentage (0-100).
        /// Extracted from "Attendibilità X-Y%" on the provider page.
        /// Defaults to 20 for probabilistic pages where the indicator is absent.
        /// Defaults to 100 for standard hourly pages without an explicit reliability indicator.
        /// </summary>
        public int ReliabilityPerc { get; set; } = 100;
    }
}
