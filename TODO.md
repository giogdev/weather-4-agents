# Weather4Agents — Implementation Checklist

## 1. Project References & NuGet Packages

### Project References
- [x] Application → Domain
- [x] Infrastructure → Application
- [x] Infrastructure → Domain
- [x] API → Application
- [x] API → Infrastructure

### NuGet Packages — Application
- [x] `MediatR`

### NuGet Packages — Infrastructure
- [x] `MediatR`
- [x] `Microsoft.Extensions.Caching.Hybrid`
- [x] `HtmlAgilityPack`
- [x] `Microsoft.Extensions.Http`
- [x] `Microsoft.Extensions.Hosting`

### NuGet Packages — API
- [x] `MediatR`

---

## 2. Cleanup scaffolding (API)
- [x] Eliminare `WeatherForecast.cs`
- [x] Eliminare `WeatherForecastController.cs`

---

## 3. Domain layer
- [x] `Enums/WeatherTypeEnums.cs` — valori aggiunti
- [x] `Entities/HoursWeatherDetails.cs` — aggiunto `TemperatureC`, rinominato `UmidityPerc` → `HumidityPerc`, `Wind` → `WindKmh`
- [x] `Entities/DayWeather.cs` — revisione (invariato)
- [x] `Entities/WeatherProvider.cs` — revisione (invariato)

---

## 4. Application layer

### Settings
- [x] `Settings/WeatherScrapingSettings.cs`

### Interfaces
- [x] `Interfaces/Scrapers/IWeatherProviderScraper.cs`
- [x] `Interfaces/Scrapers/IWeatherProviderResolver.cs`

### Use Cases
- [x] `UseCases/GetWeatherForecast/GetWeatherForecastQuery.cs`
- [x] `UseCases/GetWeatherForecast/GetWeatherForecastHandler.cs`
- [x] `UseCases/ScrapeAndCache/ScrapeAndCacheCommand.cs`
- [x] `UseCases/ScrapeAndCache/ScrapeAndCacheHandler.cs`

### DI Registration
- [x] `DependencyInjection.cs`

---

## 5. Infrastructure layer

### Scrapers
- [x] `Scrapers/Base/BaseWeatherScraper.cs`
- [x] `Scrapers/AccuWeatherScraper.cs` — stub, TODO scraping
- [x] `Scrapers/MeteoItScraper.cs` — stub, TODO scraping

### Resolvers
- [x] `Resolvers/WeatherProviderResolver.cs`

### Jobs
- [x] `Jobs/WeatherScrapingJob.cs`

### DI Registration
- [x] `DependencyInjection.cs`

---

## 6. API layer
- [x] `Controllers/WeatherController.cs`
  - [x] `GET /api/weather/{location}` (usa provider di default)
  - [x] `GET /api/weather/{location}/{provider}` (usa provider specificato)
- [x] Aggiornare `Program.cs`
- [x] Aggiornare `appsettings.json` (sezione `WeatherScraping`)

---

## Prossimi passi
- [ ] Implementare `3bMeteoScraper.ScrapeAsync` (HTML parsing con HtmlAgilityPack)

---

## 7. WeatherFileStorageJob — Salvataggio forecast su file system

### Obiettivo
Nuovo BackgroundService che salva i dati meteo su disco a intervalli configurabili.
- Path configurabile via `appsettings.json` o variabile d'ambiente Docker (`WeatherFileStorage__OutputPath`)
- Struttura directory: `{OutputPath}/{location}/{yyyy-MM-dd}.json`
- Formato file: JSON con oggetto `DayWeather` + campo `lastUpdatedAt`
- Sovrascrittura file esistenti ad ogni ciclo
- Usa il provider di default, loop su `Locations` (da `WeatherScrapingSettings`)

### File da creare
- [x] `Application/Settings/WeatherFileStorageSettings.cs` — `OutputPath`, `JobIntervalMinutes`
- [x] `Infrastructure/Models/DayWeatherFileRecord.cs` — wrapper `DayWeather` + `LastUpdatedAt`
- [x] `Infrastructure/Jobs/WeatherFileStorageJob.cs` — BackgroundService principale

### File da modificare
- [x] `Infrastructure/DependencyInjection.cs` — registrazione settings + hosted service
- [x] `API/appsettings.json` — sezione `WeatherFileStorage`

# Scarping 3bmeteo.it
- url dove fare scraping del giorno corrente: https://www.3bmeteo.com/meteo/{{location}} 
- url dove fare scraping dei giorni successivi: https://www.3bmeteo.com/meteo/{{location}}/{{day}} (dove day è un numero da 1 a 7, che indica i giorni successivi al giorno corrente)
- le informazioni delle singole ore del giorno le si possono trovare all'interno del div con la classe "row-table special_campaign"
- la struttura di una singola ora è la seguente:

```html
    <div class="row-table noPad">
        <div class="col-xs-2-3 col-sm-2-5">
            <div class="row-table">
                <div class="col-xs-1-4 big"> {{hour}}<span class="small">:00</span> </div> <div class="col-xs-1-4 text-center no-padding zoom_prv">
                    <img src="https://www.3bmeteo.com/images/set_icone/10/80-80/44.png"
                            alt="poco nuvoloso" loading="lazy" width="40" height="40">
                </div> <div class="col-xs-2-4">
                    {{weather_type_description}}
                </div>
            </div>
        </div> <div class="col-xs-1-3 col-sm-3-5 table-striped-inverse-h text-center">
            <div class="row-table ">
                <div class="col-xs-1-2 col-sm-1-5 big">
                    <span class="switchcelsius switch-te active">{{temperature_celsius}}&deg;</span> <span class="switchfahrenheit switch-te">56.5&deg;</span>
                </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDatiD-active altriDati-precipitazioni altriDatiM-active">
                    <span class="gray" aria-disabled="true">
                        {{rain_description_or_number_of_millimeters}}
                    </span>
                </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDatiD-active altriDati-venti">
                    <span class="switchkm switch-vi active">
                        {{wind_hm_h}}
                    </span> <span class="switchnodi switch-vi">
                        3
                    </span> &nbsp;{{wind_direction}}
                </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDatiD-active altriDati-umidita"> 66% </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDatiD-active altriDati-percepita">
                    <span class="switchcelsius switch-te active">14.0&deg;</span> <span class="switchfahrenheit switch-te">57.2&deg;</span>
                </div>
                <div class="col-xs-1-2 col-sm-1-5 altriDati altriDati-QN"> 2750 m <br> 3025 m </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDati-pressione"> {{pression_mbar}} </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDati-raggiuv">
                    {{sun_uv_description}}
                </div> <div class="col-xs-1-2 col-sm-1-5 altriDati altriDati-windchill">
                    <span class="switchcelsius switch-te active">13.6&deg;</span> <span class="switchfahrenheit switch-te">56.5&deg;</span>
                </div>
            </div>
        </div>
    </div>
```

# Azioni da fare
- eseguire lo scraping per i successivi 7 giorni della settimana
- Per ogni giorno estrarre l'informazione meteo (temperatura, umidità, vento, da che ora a che ora, weathertype e la descrizone testuale del meteo)
- Tutte queste informazioni dovranno compilare un ogget di tipo DayWeather (per un giorno) e HoursWeatherDetails (per ogni intervallo di ore)
