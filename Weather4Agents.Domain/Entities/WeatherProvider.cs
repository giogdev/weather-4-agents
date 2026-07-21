using System;
using System.Collections.Generic;
using System.Text;

namespace Weather4Agents.Domain.Entities
{
    /// <summary>
    /// Provider of weather data (e.g., a specific weather website or service)
    /// </summary>
    public class WeatherProvider
    {
        public WeatherProvider(string providerName)
        {
            ProviderName = providerName;
        }

        /// <summary>
        /// Name of the provider
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// IANA identifier of the timezone the provider publishes its forecasts in
        /// (e.g. "Europe/Rome"). All dates and times of the forecast are local to this timezone.
        /// Null on data cached before the field was introduced.
        /// </summary>
        public string? TimeZoneId { get; init; }
    }
}
