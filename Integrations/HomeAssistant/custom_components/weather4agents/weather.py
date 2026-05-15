"""WeatherEntity for Weather4Agents integration."""

from __future__ import annotations

import logging
from datetime import date, datetime, time, timezone
from zoneinfo import ZoneInfo

from homeassistant.components.weather import (
    Forecast,
    WeatherEntity,
    WeatherEntityFeature,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.const import UnitOfPressure, UnitOfSpeed, UnitOfTemperature
from homeassistant.core import HomeAssistant
from homeassistant.helpers.device_registry import DeviceEntryType, DeviceInfo
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import (
    CONDITION_MAP,
    CONF_LOCATION,
    DOMAIN,
    WIND_DIRECTION_MAP,
    _CONDITION_SEVERITY,
)
from .coordinator import DayForecast, HourSlot, Weather4AgentsCoordinator

_LOGGER = logging.getLogger(__name__)

# Italy timezone — the API returns local Italian times
_TZ_ITALY = ZoneInfo("Europe/Rome")


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up Weather4Agents weather entity from a config entry."""
    coordinator: Weather4AgentsCoordinator = hass.data[DOMAIN][entry.entry_id]
    async_add_entities([Weather4AgentsEntity(coordinator, entry)])


def _map_condition(weather_type: str) -> str:
    return CONDITION_MAP.get(weather_type, "exceptional")


def _worst_condition(conditions: list[str]) -> str:
    """Return the most severe HA condition from a list."""
    if not conditions:
        return "exceptional"
    return max(conditions, key=lambda c: _CONDITION_SEVERITY.index(c) if c in _CONDITION_SEVERITY else -1)


def _wind_bearing(direction: str) -> float | None:
    """Convert a compass direction string to degrees."""
    return WIND_DIRECTION_MAP.get(direction.upper())


def _slot_datetime_utc(day_str: str, time_from_str: str) -> datetime:
    """Build a UTC-aware datetime from API date + timeFrom strings."""
    # day_str: "YYYY-MM-DD", time_from_str: "HH:MM:SS"
    d = date.fromisoformat(day_str)
    t = time.fromisoformat(time_from_str)
    local_dt = datetime.combine(d, t, tzinfo=_TZ_ITALY)
    return local_dt.astimezone(timezone.utc)


def _current_slot(day: DayForecast) -> HourSlot | None:
    """Return the slot closest to (but not after) the current time, or the first available."""
    if not day.hours_details:
        return None

    now_utc = datetime.now(tz=timezone.utc)
    best: HourSlot | None = None

    for slot in day.hours_details:
        slot_dt = _slot_datetime_utc(day.date, slot.time_from)
        if slot_dt <= now_utc:
            best = slot

    # If no slot has started yet (early morning before first slot), return the first one
    return best or day.hours_details[0]


def _build_daily_forecast(day: DayForecast) -> Forecast:
    """Aggregate hourly slots into a single daily forecast entry."""
    slots = day.hours_details
    conditions = [_map_condition(s.weather_type) for s in slots]

    temps = [s.temperature_c for s in slots]
    max_temp = max(temps) if temps else None
    min_temp = min(temps) if temps else None

    avg_humidity = round(sum(s.humidity_perc for s in slots) / len(slots)) if slots else None
    avg_wind = round(sum(s.wind_kmh for s in slots) / len(slots), 1) if slots else None
    total_precip = round(sum(s.precipitation_mm for s in slots), 1) if slots else None

    # Represent the day at noon UTC
    d = date.fromisoformat(day.date)
    noon_utc = datetime(d.year, d.month, d.day, 12, 0, 0, tzinfo=timezone.utc)

    return Forecast(
        datetime=noon_utc.isoformat(),
        condition=_worst_condition(conditions),
        native_temperature=max_temp,
        native_templow=min_temp,
        native_precipitation=total_precip,
        humidity=avg_humidity,
        native_wind_speed=avg_wind,
    )


def _build_hourly_forecast(day: DayForecast, slot: HourSlot) -> Forecast:
    """Build a single hourly forecast entry from an API slot."""
    slot_dt = _slot_datetime_utc(day.date, slot.time_from)

    return Forecast(
        datetime=slot_dt.isoformat(),
        condition=_map_condition(slot.weather_type),
        native_temperature=slot.temperature_c,
        native_precipitation=slot.precipitation_mm,
        humidity=slot.humidity_perc,
        native_wind_speed=slot.wind_kmh,
        wind_bearing=_wind_bearing(slot.wind_direction),
    )


class Weather4AgentsEntity(CoordinatorEntity[Weather4AgentsCoordinator], WeatherEntity):
    """Representation of a Weather4Agents weather entity."""

    _attr_has_entity_name = True
    _attr_name = None
    _attr_native_temperature_unit = UnitOfTemperature.CELSIUS
    _attr_native_pressure_unit = UnitOfPressure.MBAR
    _attr_native_wind_speed_unit = UnitOfSpeed.KILOMETERS_PER_HOUR
    _attr_supported_features = (
        WeatherEntityFeature.FORECAST_DAILY | WeatherEntityFeature.FORECAST_HOURLY
    )

    def __init__(
        self,
        coordinator: Weather4AgentsCoordinator,
        entry: ConfigEntry,
    ) -> None:
        super().__init__(coordinator)
        self._location = entry.data[CONF_LOCATION]
        self._attr_unique_id = f"{entry.entry_id}_weather"
        self._attr_device_info = DeviceInfo(
            entry_type=DeviceEntryType.SERVICE,
            identifiers={(DOMAIN, entry.entry_id)},
            name=f"Weather4Agents — {self._location.capitalize()}",
            manufacturer="Weather4Agents",
        )

    @property
    def _today(self) -> DayForecast | None:
        if self.coordinator.data and self.coordinator.data.days:
            return self.coordinator.data.days[0]
        return None

    @property
    def _current(self) -> HourSlot | None:
        today = self._today
        return _current_slot(today) if today else None

    # --- Current conditions ---

    @property
    def condition(self) -> str | None:
        slot = self._current
        return _map_condition(slot.weather_type) if slot else None

    @property
    def native_temperature(self) -> float | None:
        slot = self._current
        return slot.temperature_c if slot else None

    @property
    def humidity(self) -> int | None:
        slot = self._current
        return slot.humidity_perc if slot else None

    @property
    def native_pressure(self) -> int | None:
        slot = self._current
        return slot.pression_mbar if slot else None

    @property
    def native_wind_speed(self) -> float | None:
        slot = self._current
        return slot.wind_kmh if slot else None

    @property
    def wind_bearing(self) -> float | None:
        slot = self._current
        return _wind_bearing(slot.wind_direction) if slot else None

    # --- Forecasts ---

    async def async_forecast_daily(self) -> list[Forecast]:
        """Return daily forecast (one entry per day)."""
        if not self.coordinator.data:
            return []
        return [_build_daily_forecast(day) for day in self.coordinator.data.days]

    async def async_forecast_hourly(self) -> list[Forecast]:
        """Return hourly forecast (one entry per slot, all days)."""
        if not self.coordinator.data:
            return []
        result: list[Forecast] = []
        for day in self.coordinator.data.days:
            for slot in day.hours_details:
                result.append(_build_hourly_forecast(day, slot))
        return result
