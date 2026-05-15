"""Config flow for Weather4Agents integration."""

from __future__ import annotations

import logging
from typing import Any

import aiohttp
import voluptuous as vol

from homeassistant.config_entries import ConfigFlow, ConfigFlowResult
from homeassistant.core import HomeAssistant
from homeassistant.exceptions import HomeAssistantError

from .const import (
    CONF_BASE_URL,
    CONF_LOCATION,
    CONF_SCAN_INTERVAL,
    DEFAULT_LOCATION,
    DEFAULT_SCAN_INTERVAL,
    DOMAIN,
)

_LOGGER = logging.getLogger(__name__)

STEP_USER_DATA_SCHEMA = vol.Schema(
    {
        vol.Required(CONF_BASE_URL, description={"suggested_value": "http://192.168.1.10:8080"}): str,
        vol.Required(CONF_LOCATION, default=DEFAULT_LOCATION): str,
        vol.Optional(CONF_SCAN_INTERVAL, default=DEFAULT_SCAN_INTERVAL): vol.All(
            int, vol.Range(min=5, max=1440)
        ),
    }
)


async def _validate_connection(hass: HomeAssistant, data: dict[str, Any]) -> None:
    """Validate that the API is reachable and the location exists."""
    base_url = data[CONF_BASE_URL].rstrip("/")
    location = data[CONF_LOCATION]
    url = f"{base_url}/api/weather/{location}/forecast/week"

    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(url, timeout=aiohttp.ClientTimeout(total=10)) as resp:
                if resp.status == 404:
                    raise InvalidLocation
                if resp.status != 200:
                    raise CannotConnect
    except aiohttp.ClientError as err:
        raise CannotConnect from err


class Weather4AgentsConfigFlow(ConfigFlow, domain=DOMAIN):
    """Handle a config flow for Weather4Agents."""

    VERSION = 1

    async def async_step_user(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        errors: dict[str, str] = {}

        if user_input is not None:
            try:
                await _validate_connection(self.hass, user_input)
            except CannotConnect:
                errors["base"] = "cannot_connect"
            except InvalidLocation:
                errors[CONF_LOCATION] = "invalid_location"
            except Exception:
                _LOGGER.exception("Unexpected error during Weather4Agents setup")
                errors["base"] = "unknown"
            else:
                title = f"Weather4Agents — {user_input[CONF_LOCATION].capitalize()}"
                return self.async_create_entry(title=title, data=user_input)

        return self.async_show_form(
            step_id="user",
            data_schema=STEP_USER_DATA_SCHEMA,
            errors=errors,
        )


class CannotConnect(HomeAssistantError):
    """Error raised when the API is unreachable."""


class InvalidLocation(HomeAssistantError):
    """Error raised when the location is not found in the API."""
