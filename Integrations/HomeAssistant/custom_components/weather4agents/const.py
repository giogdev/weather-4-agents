"""Constants for the Weather4Agents integration."""

DOMAIN = "weather4agents"

CONF_BASE_URL = "base_url"
CONF_LOCATION = "location"
CONF_SCAN_INTERVAL = "scan_interval"

DEFAULT_LOCATION = "nembro"
DEFAULT_SCAN_INTERVAL = 60  # minutes

# Severity order for picking the "worst" condition of the day in daily forecast.
# Higher index = more severe.
_CONDITION_SEVERITY = [
    "sunny",
    "partlycloudy",
    "cloudy",
    "windy",
    "windy-variant",
    "fog",
    "rainy",
    "pouring",
    "snowy",
    "snowy-rainy",
    "hail",
    "lightning-rainy",
    "exceptional",
]

CONDITION_MAP: dict[str, str] = {
    "Unknown": "exceptional",
    "Sunny": "sunny",
    "PartlyCloudy": "partlycloudy",
    "LightClouds": "partlycloudy",
    "Cloudy": "cloudy",
    "Overcast": "cloudy",
    "Foggy": "fog",
    "Rainy": "rainy",
    "ProbablyRainy": "rainy",
    "HeavyRain": "pouring",
    "Thunderstorm": "lightning-rainy",
    "Snowy": "snowy",
    "HeavySnow": "snowy",
    "Sleet": "snowy-rainy",
    "Hail": "hail",
    "Windy": "windy",
    "HeavyWindy": "windy-variant",
}

WIND_DIRECTION_MAP: dict[str, float] = {
    "N": 0.0,
    "NNE": 22.5,
    "NE": 45.0,
    "ENE": 67.5,
    "E": 90.0,
    "ESE": 112.5,
    "SE": 135.0,
    "SSE": 157.5,
    "S": 180.0,
    "SSO": 202.5,
    "SSW": 202.5,
    "SO": 225.0,
    "SW": 225.0,
    "OSO": 247.5,
    "WSW": 247.5,
    "O": 270.0,
    "W": 270.0,
    "ONO": 292.5,
    "WNW": 292.5,
    "NO": 315.0,
    "NW": 315.0,
    "NNO": 337.5,
    "NNW": 337.5,
}
