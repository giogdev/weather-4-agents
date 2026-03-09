# Piano: Home Assistant Integration per Weather4Agents

## Context
Sviluppare un'integrazione custom Python per Home Assistant che consuma le API REST di Weather4Agents (running in Docker su homelab) per popolare un'entità `weather` nativa di HA, con forecast giornaliero (7 giorni) e orario.

---

## File da creare
**Directory:** `Integrations/HomeAssistant/custom_components/weather4agents/`

| File | Scopo |
|------|-------|
| `manifest.json` | Metadata integrazione (domain, iot_class: local_polling, requirements) |
| `const.py` | Costanti (DOMAIN, DEFAULT_SCAN_INTERVAL, mapping condizioni) |
| `config_flow.py` | UI config flow: chiede base_url, city, scan_interval |
| `coordinator.py` | DataUpdateCoordinator: effettua la chiamata API e aggrega dati |
| `weather.py` | WeatherEntity con FORECAST_DAILY + FORECAST_HOURLY |
| `__init__.py` | `async_setup_entry` + `async_unload_entry` |
| `strings.json` | Testi UI (italiano + inglese) |
| `README.md` | Istruzioni installazione (copiare in HA custom_components/) |

---

## Modifica API (prerequisito)

**File:** `Weather4Agents.Application/UseCases/GetWeekForecast/GetWeekForecastHandler.cs`

Cambiare una riga nel handler per includere oggi nel forecast:
```csharp
// Prima (esclude oggi):
.Where(d => d.Date > today)

// Dopo (include oggi):
.Where(d => d.Date >= today)
```
Risultato: l'endpoint ritorna oggi + 6 giorni successivi (7 totali). Questo elimina la necessità di una seconda chiamata API dall'integrazione HA.

---

## Architettura

### Config Flow (config_flow.py)
Parametri configurabili dall'utente via UI HA:
- `base_url`: URL completo delle API (es. `http://192.168.1.10:8080`)
- `location`: nome città (default: `nembro`)
- `scan_interval`: intervallo aggiornamento in minuti (default: 60)

### DataUpdateCoordinator (coordinator.py)
Effettua **1 sola chiamata API** ad ogni aggiornamento:
- `GET {base_url}/api/weather/{location}/forecast/week` → oggi + 6 giorni successivi

Il primo giorno del risultato è oggi → usato per le condizioni correnti.
I giorni successivi → usati per il forecast settimanale.
Dati messi in cache; le entità leggono solo dalla cache.

### WeatherEntity (weather.py)
- **Stato corrente**: slot orario più vicino all'ora attuale dai dati di oggi
  - `native_temperature`: TemperatureC dello slot corrente
  - `humidity`: HumidityPerc
  - `native_pressure`: PressionMbar
  - `wind_speed`: WindKmh
  - `wind_bearing`: WindDirection (mappato a gradi)
  - `condition`: WeatherType → HA condition string
- **Feature flags**: `WeatherEntityFeature.FORECAST_DAILY | WeatherEntityFeature.FORECAST_HOURLY`
- **`async_forecast_daily()`** / **`async_forecast_hourly()`**: ritorna daily o hourly

### Forecast Daily
Per ogni giorno della settimana (7 entry), aggrega gli slot orari:
- `datetime`: data alle 12:00 UTC (RFC 3339)
- `condition`: condizione più "severa" della giornata
- `native_temperature`: temperatura massima del giorno
- `native_templow`: temperatura minima del giorno
- `native_precipitation`: somma precipitazioni del giorno
- `native_wind_speed`: media WindKmh
- `humidity`: media HumidityPerc

### Forecast Hourly
Per ogni slot `HoursWeatherDetails` (tutti i giorni, oggi + 6):
- `datetime`: date + TimeFrom convertito da `Europe/Rome` a UTC (RFC 3339)
- `condition`: WeatherType mappato
- `native_temperature`: TemperatureC
- `native_precipitation`: PrecipitationMm
- `humidity`: HumidityPerc
- `native_wind_speed`: WindKmh
- `wind_bearing`: WindDirection → gradi

---

## Mapping condizioni (const.py)
```python
CONDITION_MAP = {
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
```

---

## manifest.json
```json
{
  "domain": "weather4agents",
  "name": "Weather4Agents",
  "codeowners": [],
  "documentation": "",
  "version": "1.0.0",
  "integration_type": "service",
  "iot_class": "local_polling",
  "requirements": [],
  "config_flow": true
}
```
*(Nessun requirement esterno: usa solo `aiohttp` già disponibile in HA)*

---

## Installazione
1. Copiare `custom_components/weather4agents/` in `<ha_config>/custom_components/`
2. Riavviare HA
3. Impostazioni → Integrazioni → Aggiungi → "Weather4Agents"
4. Inserire URL API, città, intervallo

**Nota Docker networking:** usare l'IP dell'host (non `localhost`) o il nome del container se condividono la stessa rete Docker.

---

## Verifica / Test
1. Verificare che l'API risponda: `curl http://<host>:8080/api/weather/nembro/forecast/week`
2. Dopo installazione in HA: controllare che `weather.weather4agents_nembro` appaia in Impostazioni → Integrazioni
3. Verificare stato corrente e forecast in Developer Tools → Stati
4. Testare card Lovelace:
   ```yaml
   type: weather-forecast
   entity: weather.weather4agents_nembro
   forecast_type: daily
   ```
5. Controllare i log HA per errori di polling: Impostazioni → Sistema → Log
