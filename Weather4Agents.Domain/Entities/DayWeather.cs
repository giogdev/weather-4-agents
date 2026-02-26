using System;
using System.Collections.Generic;
using System.Text;

namespace Weather4Agents.Domain.Entities
{

    public class DayWeather
    {
        public DateOnly Date { get; set; }
        public List<HoursWeatherDetails> HoursDetails { get; set; } = new List<HoursWeatherDetails>();
        public WeatherProvider Provider { get; set; } = new WeatherProvider("NotSet");
    }
}
