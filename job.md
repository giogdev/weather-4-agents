# Job e automatismi
In questa pagina sono descritti i job 

## _WeatherScrapingJob_: scraping automatico
**Può essere disabilitato**: ❌

`WeatherScrapingJob` è un job automatico che gira ad un intervallo configurabile e si occupa di aggiornare i dati in cache del meteo. In questo modo tutte le funzionalità ell'applicazione leggeranno i dati pre-elaborati anzichè leggerli da web

## _StorageJob_: salvataggio forecast su file system
**Può essere disabilitato**: ✅

`WeatherFileStorageJob` è un background service opzionale che salva periodicamente i dati meteo come file JSON sul file system. È pensato per ambienti Docker dove gli agenti possono leggere i file direttamente, senza dover chiamare l'API.

**Struttura delle directory:**

```
{OutputPath}/
└── {location}/
    ├── 2026-02-26.json
    ├── 2026-02-27.json
    └── ...
```

**Formato del file:**

```json
{
  "lastUpdatedAt": "2026-02-26T09:00:00+00:00",
  "weather": {
    "date": "2026-02-26",
    "provider": { "providerName": "3bMeteo" },
    "hoursDetails": [
      {
        "timeFrom": "00:00:00",
        "timeTo": "01:00:00",
        "weatherType": "partlyCloudy",
        "weatherTypeDescription": "poco nuvoloso",
        "temperatureC": 8.5,
        "precipitationsPerc": 0,
        "humidityPerc": 66,
        "pressionMbar": 1012,
        "windKmh": 5.0,
        "windDirection": "NE"
      }
    ]
  }
}
```

Il job sovrascrive i file esistenti ad ogni ciclo. Usa il provider di default e legge le città dalla sezione `WeatherScraping:Locations`.

### Configurazione

La funzionalità è **disabilitata per default**. Per attivarla, impostare `Enabled: true` nella sezione `WeatherFileStorage` dell'`appsettings.json`:

```json
"WeatherFileStorage": {
  "Enabled": true,
  "OutputPath": "weather-data",
  "JobIntervalMinutes": 60,
  "CleanupEnabled": false
}
```

## "StorageJob": pulizia automatica dei file vecchi
**Può essere disabilitato**: ✅

Quando `CleanupEnabled` è `true`, al termine di ogni ciclo il job scorre tutte le sottodirectory di `OutputPath` ed elimina i file `.json` il cui nome (formato `yyyy-MM-dd`) è **anteriore a ieri**. I file di oggi e di ieri vengono sempre mantenuti.

Gli errori di cancellazione sono loggati come `Warning` senza interrompere il ciclo.
> ⚠️ I file che non rispettano il formato di nome `yyyy-MM-dd` vengono ignorati. 
