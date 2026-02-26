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
        public int PrecipitationsPerc { get; set; }
        public int HumidityPerc { get; set; }
        public int PressionMbar { get; set; }
        public double WindKmh { get; set; }
        public string WindDirection { get; set; } = "";
    }
}
