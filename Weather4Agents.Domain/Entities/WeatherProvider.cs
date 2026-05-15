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
        public string ProviderName { get; set; }
    }
}
