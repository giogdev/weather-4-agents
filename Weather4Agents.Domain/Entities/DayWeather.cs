using System;
using System.Collections.Generic;
using System.Text;

namespace Weather4Agents.Domain.Entities
{
    /// <summary>
    /// Forecast of a single day
    /// </summary>
    public class DayWeather
    {
        /// <summary>
        /// Date of the forecast
        /// </summary>
        public DateOnly Date { get; set; }
        /// <summary>
        /// List of weather details for each hour of the day
        /// </summary>
        public List<HoursWeatherDetails> HoursDetails { get; set; } = new List<HoursWeatherDetails>();
        /// <summary>
        /// Weather provider where date come from
        /// </summary>
        public WeatherProvider Provider { get; set; } = new WeatherProvider("NotSet");
        /// <summary>
        /// Overall forecast reliability percentage (0-100) as reported by the provider.
        /// Defaults to 100 when the provider does not expose a reliability indicator.
        /// </summary>
        public int ReliabilityPerc { get; set; } = 100;
    }
}
