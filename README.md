# Weather4Agents 🤖

Tool sviluppato con lo scopo di leggere i dati metereologici dal proprio sito web preferito.

Utilizzo questo tool per mostrare i dati in **Home Assistant** e per fornire velocemente dati ai miei agenti ( es. 🦞 **OpenClaw** ), senza consumare troppi token.
Per questo secondo scopo generò dei file JSON con i dati meteo, in modo tale che i miei agenti sappiano dove recuperare i dati.

## Funzionalità

- **API REST** — forecast multi-giorno e meteo per singolo giorno, con supporto a provider multipli.
- **Salvataggio su file system** — Salva il forecast in file json sul file system, vedi sezione dedicata.

Provider disponibili:

| Provider | Nome | Stato |
|---|---|---|
| [3bMeteo](https://www.3bmeteo.com) | `3bMeteo` | ✅ Implementato |

## Avvio

1. Configura il file docker-compose.yml con le informazioni che ti servono
1. Avvia il docker compose tramite il comando:
    ```bash
    docker-compose up -d
    ```

> ℹ️ Vedi [la documentazione](docs/docker.md) per ulteriori dettagli su Docker

## Parametri configurabili (docker compose)

### WeatherScraping

| Variabile d'ambiente | Default | Descrizione |
|---|---|---|
| `WeatherScraping__DefaultProvider` | — | Provider di default (es. `3bMeteo`) |
| `WeatherScraping__EnabledProviders__0` | — | Lista provider abilitati (es. `3bMeteo`) |
| `WeatherScraping__Locations__0` | — | Lista location da monitorare (es. `Milano`) |
| `WeatherScraping__JobIntervalMinutes` | `60` | Intervallo del job di scraping in minuti |

### WeatherFileStorage

| Variabile d'ambiente | Default | Descrizione |
|---|---|---|
| `WeatherFileStorage__Enabled` | `false` | Abilita/disabilita il job di salvataggio su file |
| `WeatherFileStorage__OutputPath` | `weather-data` | Directory root dove vengono scritti i file JSON |
| `WeatherFileStorage__JobIntervalMinutes` | `60` | Intervallo del job di salvataggio in minuti |
| `WeatherFileStorage__CleanupEnabled` | `false` | Se `true`, elimina i file JSON con data antecedente a ieri ad ogni ciclo |

## Job automatici
[Vedi qui la documentazione](docs/job.md)