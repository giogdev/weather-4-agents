using System;
using System.Collections.Generic;
using System.Text;

namespace Weather4Agents.Domain.Entities
{
    public class WeatherProvider
    {
        public WeatherProvider(string providerName)
        {
            ProviderName = providerName;
        }

        public string ProviderName { get; set; }
    }
}
