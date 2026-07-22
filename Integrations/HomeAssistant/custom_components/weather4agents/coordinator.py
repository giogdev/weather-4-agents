"""DataUpdateCoordinator for Weather4Agents."""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from datetime import timedelta

import aiohttp

from homeassistant.core import HomeAssistant
from homeassistant.helpers.aiohttp_client import async_get_clientsession
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator, UpdateFailed

from .const import DOMAIN, forecast_week_url

_LOGGER = logging.getLogger(__name__)


@dataclass
class HourSlot:
    """A single hourly weather slot from the API."""

    time_from: str
    time_to: str
    weather_type: str
    weather_type_description: str
    temperature_c: float
    precipitation_mm: float
    humidity_perc: int
    pression_mbar: int
    wind_kmh: float
    wind_direction: str


@dataclass
class DayForecast:
    """A single day forecast entry from the API."""

    date: str  # "YYYY-MM-DD"
    # Overall forecast reliability (0-100). The API exposes this per day, not per hour.
    reliability_perc: int = 100
    hours_details: list[HourSlot] = field(default_factory=list)


@dataclass
class Weather4AgentsData:
    """Aggregated data returned by the coordinator."""

    last_updated_at: str
    days: list[DayForecast] = field(default_factory=list)


def _parse_slot(raw: dict) -> HourSlot:
    return HourSlot(
        time_from=raw.get("timeFrom", "00:00:00"),
        time_to=raw.get("timeTo", "00:00:00"),
        weather_type=raw.get("weatherType", "Unknown"),
        weather_type_description=raw.get("weatherTypeDescription", ""),
        temperature_c=float(raw.get("temperatureC", 0)),
        precipitation_mm=float(raw.get("precipitationMm", 0)),
        humidity_perc=int(raw.get("humidityPerc", 0)),
        pression_mbar=int(raw.get("pressionMbar", 0)),
        wind_kmh=float(raw.get("windKmh", 0)),
        wind_direction=raw.get("windDirection", ""),
    )


def _parse_response(data: dict) -> Weather4AgentsData:
    days: list[DayForecast] = []
    for day_raw in data.get("forecast", []):
        date = day_raw.get("date")
        if not date:
            _LOGGER.warning("Skipping forecast day without a date: %s", day_raw)
            continue
        try:
            slots = [_parse_slot(s) for s in day_raw.get("hoursDetails", [])]
            days.append(
                DayForecast(
                    date=date,
                    reliability_perc=int(day_raw.get("reliabilityPerc", 100)),
                    hours_details=slots,
                )
            )
        except (ValueError, TypeError) as err:
            _LOGGER.warning("Skipping malformed forecast day %s: %s", date, err)
            continue

    return Weather4AgentsData(
        last_updated_at=data.get("lastUpdatedAt", ""),
        days=days,
    )


class Weather4AgentsCoordinator(DataUpdateCoordinator[Weather4AgentsData]):
    """Coordinator that fetches weather data from Weather4Agents API."""

    def __init__(
        self,
        hass: HomeAssistant,
        base_url: str,
        location: str,
        scan_interval_minutes: int,
    ) -> None:
        self.base_url = base_url.rstrip("/")
        self.location = location

        super().__init__(
            hass,
            _LOGGER,
            name=DOMAIN,
            update_interval=timedelta(minutes=scan_interval_minutes),
        )

    async def _async_update_data(self) -> Weather4AgentsData:
        url = forecast_week_url(self.base_url, self.location)

        session = async_get_clientsession(self.hass)
        try:
            async with session.get(url, timeout=aiohttp.ClientTimeout(total=30)) as resp:
                if resp.status != 200:
                    raise UpdateFailed(
                        f"Weather4Agents API returned HTTP {resp.status} for {url}"
                    )
                data = await resp.json()
        except aiohttp.ClientError as err:
            raise UpdateFailed(f"Error communicating with Weather4Agents API: {err}") from err

        return _parse_response(data)
