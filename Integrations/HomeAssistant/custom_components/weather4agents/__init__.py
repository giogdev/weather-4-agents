"""Weather4Agents integration for Home Assistant."""

from __future__ import annotations

from homeassistant.config_entries import ConfigEntry
from homeassistant.const import Platform
from homeassistant.core import HomeAssistant

from .const import CONF_BASE_URL, CONF_LOCATION, CONF_SCAN_INTERVAL, DOMAIN
from .coordinator import Weather4AgentsCoordinator

PLATFORMS: list[Platform] = [Platform.WEATHER]


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up Weather4Agents from a config entry."""
    coordinator = Weather4AgentsCoordinator(
        hass=hass,
        base_url=entry.data[CONF_BASE_URL],
        location=entry.data[CONF_LOCATION],
        scan_interval_minutes=entry.data[CONF_SCAN_INTERVAL],
    )

    await coordinator.async_config_entry_first_refresh()

    hass.data.setdefault(DOMAIN, {})[entry.entry_id] = coordinator

    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)

    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload a config entry."""
    unload_ok = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)

    if unload_ok:
        hass.data[DOMAIN].pop(entry.entry_id)

    return unload_ok
