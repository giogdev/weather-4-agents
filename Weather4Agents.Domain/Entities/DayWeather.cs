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
    }
}
