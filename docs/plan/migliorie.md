# Piano di migliorie — Weather4Agents

> Esito dell'analisi approfondita del 2026-07-19.
> Stato di partenza: build OK (0 errori, 2 warning NU1903), 25/25 test verdi.
> Le voci sono ordinate per priorità. Ogni voce indica file e riga di riferimento.

---

## 🔴 Fase 1 — Sicurezza urgente

### S1. Password MQTT committata nella history del repository
`Weather4Agents.API/appsettings.Development.json` contiene una password MQTT in chiaro
(`Integrations:HomeAssistant:MqttPassword`). Il file oggi è in `.gitignore`, ma **è stato
committato in passato** (commit `68eacb3` e `617f9cb`) e quindi la password è recuperabile
dalla history di un repository pubblico.

Inoltre la sezione `Integrations` **non è letta da nessun codice** (nessun riferimento a MQTT
nella solution): è configurazione morta.

- [ ] Ruotare immediatamente la password sul broker MQTT
- [ ] Rimuovere la sezione `Integrations` da `appsettings.Development.json`
- [ ] Valutare la riscrittura della history (`git filter-repo`) o considerare il segreto compromesso
- [ ] Per eventuali segreti futuri: User Secrets in dev (`UserSecretsId` già presente nel csproj) e variabili d'ambiente in prod

### S2. Vulnerabilità nota in Microsoft.OpenApi 2.0.0 (NU1903, high severity)
La build segnala GHSA-v5pm-xwqc-g5wc via dipendenza transitiva di
`Microsoft.AspNetCore.OpenApi 10.0.3` (`Weather4Agents.API/Weather4Agents.API.csproj`).

- [ ] Aggiornare `Microsoft.AspNetCore.OpenApi` all'ultima patch oppure aggiungere un riferimento esplicito a `Microsoft.OpenApi` >= versione corretta
- [ ] Aggiungere in CI un check dedicato (`dotnet list package --vulnerable` o Dependabot)

### S3. API completamente aperta e senza rate limiting
`Program.cs:52-53`: HTTPS redirect e authorization commentati. Nessuna API key, nessun
rate limiting. Ogni richiesta con una location non in cache **innesca fino a 8 richieste HTTP
verso 3bmeteo** e crea una nuova entry di cache da 24h: chiunque raggiunga l'endpoint può
saturare memoria (entry illimitate) e far bombardare il sito terzo (vedi anche B7).

- [ ] Aggiungere `AddRateLimiter` (fixed window per IP) sugli endpoint weather
- [ ] Opzionale: API key semplice via header (configurabile, off di default per uso LAN)
- [ ] Valutare una whitelist: servire solo le location configurate in `WeatherScraping:Locations` (o renderlo opt-in con un flag `AllowUnconfiguredLocations`)

---

## 🟠 Fase 2 — Bug di correttezza

### B1. Risultati vuoti cacheati per 24h e contratto 404 mai rispettato
`BaseWeatherScraper.GetForecastAsync` (`Scrapers/Base/BaseWeatherScraper.cs:36-39`) cachea
qualunque risultato di `ScrapeAsync`, **anche una lista vuota**: se 3bmeteo è irraggiungibile
o la location non esiste (tutte le `HttpRequestException` sono inghiottite in
`3bMeteoScraper.cs:49-52`), l'API risponde `200 OK` con corpo vuoto **per le successive 24 ore**.

Conseguenze a catena:
- Gli endpoint dichiarano `404` (`WeatherController.cs`) ma per una location inesistente restituiscono sempre `200` vuoto
- Il config flow di Home Assistant (`config_flow.py:46-47`) valida la location aspettandosi un 404 → `invalid_location` non scatta mai e si possono configurare location inesistenti

Correzioni proposte:
- [ ] Non cachear il risultato quando la lista è vuota (o cachearlo con TTL breve, es. 5 min, come negative-cache)
- [ ] Restituire `404` dai query handler / controller quando il forecast è vuoto (introduce un'eccezione dedicata tipo `LocationNotFoundException` invece di riusare `InvalidOperationException`)
- [ ] Aggiornare il config flow HA di conseguenza (o considerare "forecast vuoto" = location invalida)

### B2. Errori di scraping inghiottiti senza log
`3bMeteoScraper.cs:49-52`: `catch (HttpRequestException) { }` — nessun logger in tutto lo
scraper. Un guasto al parsing o al sito è invisibile finché qualcuno non nota dati mancanti.
Inoltre `TaskCanceledException` da timeout HTTP **non** è catturata: un timeout su un giorno
fa fallire l'intero scrape (incoerente con la strategia "salta il giorno").

- [ ] Iniettare `ILogger<Meteo3bScraper>` (via `BaseWeatherScraper`) e loggare warning con URL e motivo
- [ ] Gestire anche timeout (`TaskCanceledException` quando `ct` non è cancellato)
- [ ] Loggare a livello `Warning` quando `MapWeatherType` restituisce `Unknown`, con la descrizione originale (oggi lo fa solo l'integrazione HA)

### B3. Timezone: date e orari dipendono dal fuso del server
Lo scraper etichetta i giorni con `DateTime.Today` (`3bMeteoScraper.cs:30`), e gli handler
usano `DateTime.Now`/`DateTime.Today` (`GetNext24HoursForecastHandler.cs:24`,
`GetWeekForecastHandler.cs:24`). I dati di 3bmeteo sono in ora italiana, ma in un container
Docker (UTC) intorno alla mezzanotte le date risultano **sfalsate di un giorno** e la
finestra "next 24h" è spostata di 1-2 ore. L'integrazione HA assume esplicitamente
`Europe/Rome` (`weather.py:33`), quindi il contratto implicito è "ora italiana".

- [ ] Centralizzare il calcolo di "adesso/oggi" su `TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")` (o meglio: timezone per-provider, dato che è un attributo della fonte)
- [ ] Usare `TimeProvider` iniettato invece di `DateTime.Now` statico (testabilità, vedi T2)
- [ ] Documentare/esportare la timezone nelle risposte API (vedi L8)

### B4. Mapping condizioni meteo: plurale "piogge" non mappato e regole irraggiungibili
`3bMeteoScraper.cs:361-380` (`WeatherMappings`):
- La regola Rainy controlla `"pioggia"`, `"rovescio"`, `"rovesci"` e **`"pioggere"`** (probabile typo di `"piogge"`). Descrizioni come *"nubi sparse con piogge"* o *"piogge diffuse"* non matchano le regole pioggia → finiscono su `PartlyCloudy` o `Unknown`
- `"vento forte"` (HeavyWindy) e `"velature"` (LightClouds) sono in coda alla lista: quasi sempre pre-empted da `Cloudy`/`Sunny` (es. *"sereno con velature"* → Sunny). Documentare se voluto o riordinare
- `WeatherType.Windy` non è mai assegnato da nessuna regola
- Manca `"pioviggine"` / `"pioggia debole"` → LightRain

- [ ] Correggere `"pioggere"` → `"piogge"` valutando l'ordine (le combo `"possibili piogge"` devono restare prioritarie: spostare le regole "possibili piogge" **prima** della regola Rainy)
- [ ] Aggiungere test per: "nubi sparse con piogge", "piogge diffuse", "pioviggine", "sereno con velature", "vento forte"
- [ ] Estrarre il mapping in una classe testabile separata (oggi è testato solo via reflection su `ParseDayPage`)

### B5. Nessuna validazione delle impostazioni: intervalli ≤ 0 fermano l'host
`WeatherScrapingJob.cs:32` e `WeatherFileStorageJob.cs:57`: `Task.Delay(TimeSpan.FromMinutes(x))`
con `JobIntervalMinutes` negativo lancia `ArgumentOutOfRangeException` → con il comportamento
di default (`BackgroundServiceExceptionBehavior.StopHost`) **l'intera applicazione si spegne**.
Con `0` si ottiene un loop senza pausa che martella 3bmeteo.

- [ ] Aggiungere DataAnnotations alle classi Settings (`Range(1, 1440)`, `Required` su `DefaultProvider`, ecc.)
- [ ] Registrare con `AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`
- [ ] Validare che `DefaultProvider` ∈ `EnabledProviders` all'avvio

### B6. Input API non validati
`WeatherController.GetForecastByDays`: `numberOfDays` negativo o 0 → `200` con lista vuota;
nessun limite superiore. `location` non è validata (lunghezza, caratteri).

- [ ] `numberOfDays` fuori da [1, 8] → `400 Bad Request` (documentare il massimo)
- [ ] Validare `location` (regex semplice: lettere, spazi, apostrofi, trattini; lunghezza max) → `400`
- [ ] Aggiungere `ProducesResponseType(400)` alle annotation OpenAPI

### B7. Chiave di cache non normalizzata + crescita illimitata
`BaseWeatherScraper.cs:27`: la chiave usa `location.ToLowerInvariant()` ma l'URL di scraping
normalizza gli spazi in trattini (`3bMeteoScraper.cs:31`). `san pellegrino terme` e
`san-pellegrino-terme` producono **due entry di cache e due scrape** per la stessa pagina.
Con location arbitrarie dagli endpoint pubblici le entry crescono senza limite (24h TTL).

- [ ] Normalizzare la location una sola volta (trim, lowercase, spazi→trattini) e usarla sia come chiave sia nell'URL
- [ ] Insieme a S3 (whitelist/rate limit) per limitare la cardinalità

### B8. Scrittura file JSON non atomica
`WeatherFileStorageJob.cs:109`: `File.WriteAllTextAsync` sovrascrive in place. Un agente che
legge il file nello stesso istante può leggere JSON troncato — proprio il caso d'uso primario
del progetto (agenti che leggono i file).

- [ ] Scrivere su file temporaneo nella stessa directory + `File.Move(tmp, dest, overwrite: true)` (rename atomico sullo stesso filesystem)

### B9. `LastUpdatedAt` indica l'ora della risposta, non la freschezza dei dati
`GetNext24HoursForecastHandler.cs:51` e `GetWeekForecastHandler.cs:38` impostano
`DateTimeOffset.UtcNow` alla generazione della risposta: dati vecchi di 23 ore risultano
"aggiornati adesso". Stesso problema nei file su disco (`WeatherFileStorageJob.cs:94`).

- [ ] Salvare il timestamp dello scrape insieme ai dati (es. wrapper `CachedForecast { ScrapedAt, Days }` in cache) e propagarlo come `LastUpdatedAt`

### B10. Path Windows hardcoded in configurazione e docker-compose
- `appsettings.json:18`: `"OutputPath": "C:\\WeatherData"` — in un container Linux crea una directory **letteralmente chiamata** `C:\WeatherData` nella working dir
- `docker/docker-compose.yml`: volume `C:\WeatherData:/app/weather-data` — rompe il compose per chiunque non sia su Windows

- [ ] Default relativo `weather-data` in `appsettings.json` (coerente col default della classe `WeatherFileStorageSettings`)
- [ ] Volume relativo `./weather-data:/app/weather-data` nel compose (con nota nel README per Windows)

### B11. Bug nell'integrazione Home Assistant
`Integrations/HomeAssistant/custom_components/weather4agents/`:
- `coordinator.py:64`: legge `reliabilityPerc` a livello di **slot**, ma l'API lo espone a livello di **giorno** (`DayForecastEntry`) → sempre 100 e mai usato
- `coordinator.py:72`: `day_raw["date"]` senza guardia → `KeyError` non gestito fa fallire l'update
- `coordinator.py:101` e `config_flow.py:41`: la location non è URL-encoded (una location con spazi rompe l'URL)
- `config_flow.py`: manca `async_set_unique_id` → si possono creare entry duplicate per la stessa location/URL
- `coordinator.py:104`: crea una `aiohttp.ClientSession` per richiesta invece di usare `async_get_clientsession(hass)` (best practice HA)
- `weather.py:213-221`: il forecast orario include anche gli slot già passati di oggi

- [ ] Correggere i punti sopra; per reliability: propagare il valore day-level in `DayForecast` se si vuole esporlo come attributo

### B12. HttpClient senza timeout né resilienza
`AddHttpClient<Meteo3bScraper>()` senza configurazione: timeout default 100 s **per richiesta**,
8 richieste sequenziali per location → un ciclo può bloccarsi ~13 minuti. Nessun retry per
errori transitori.

- [ ] Impostare `client.Timeout` ~10-15 s
- [ ] Aggiungere `AddStandardResilienceHandler()` (Microsoft.Extensions.Http.Resilience): retry con backoff + circuit breaker
- [ ] Valutare il parallelismo limitato dei fetch dei giorni (es. `Task.WhenAll` con throttling a 2-3) mantenendo un comportamento "polite" verso il sito

### B13. Caricamento configurazione basato su `#if DEBUG/RELEASE`
`Program.cs:8-14`: anti-pattern. `appsettings.Development.json` è già caricato dall'host
quando `ASPNETCORE_ENVIRONMENT=Development` (quindi in dev viene caricato **due volte**), e
una build Debug eseguita con environment Production caricherebbe comunque i settings di dev.

- [ ] Rimuovere i blocchi `#if` e affidarsi al meccanismo standard `appsettings.{Environment}.json`
- [ ] Rimuovere il blocco `if (app.Environment.IsDevelopment()) { }` vuoto (`Program.cs:40-43`)
- [ ] Decidere se Scalar/OpenAPI devono essere esposti anche in produzione (probabilmente sì per questo progetto, ma sia una scelta esplicita)

---

## 🟡 Fase 3 — Robustezza e lacune funzionali

### L1. Gestione errori globale con ProblemDetails
I controller catturano solo `InvalidOperationException` (usata sia per "provider inesistente"
sia potenzialmente da altre librerie). Ogni altra eccezione → 500 grezzo.

- [ ] `builder.Services.AddProblemDetails()` + `app.UseExceptionHandler()`
- [ ] Eccezioni di dominio dedicate (`ProviderNotFoundException`, `LocationNotFoundException`) mappate a 404/400 in un solo punto, rimuovendo i try/catch ripetuti nei controller

### L2. Health check
Nessun endpoint di health: utile per Docker (`HEALTHCHECK`), per HA e per orchestratori.

- [ ] `AddHealthChecks()` + `MapHealthChecks("/health")`
- [ ] Check custom: ultima esecuzione riuscita del job di scraping entro N minuti
- [ ] `HEALTHCHECK` nel `Dockerfile`

### L3. Coordinamento fra i due job e file storage multi-provider
`WeatherFileStorageJob` salva solo i dati del provider **di default** e riparte in parallelo
allo scraping job all'avvio (il primo ciclo può auto-innescare uno scrape). I file non
distinguono il provider (gap rispetto al design multi-provider).

- [ ] Opzione A (semplice): far scrivere i file direttamente al termine del ciclo di scraping (un solo job, un solo intervallo)
- [ ] Opzione B: mantenere due job ma iterare su `EnabledProviders` e organizzare i file come `{OutputPath}/{location}/{provider}/{date}.json` (breaking per i consumatori: documentare)

### L4. Cache persa a ogni riavvio
Solo L1 in-memory: ogni restart ⇒ re-scrape completo di tutte le location.

- [ ] Valutare un L2 economico: riusare i file JSON già scritti come fonte di bootstrap, oppure `HybridCache` con distributed cache su file/SQLite. Basso sforzo, evita hammering al riavvio

### L5. Semantica timezone assente nelle risposte API
`TimeFrom`/`TimeTo`/`Date` sono ora italiana implicita, `LastUpdatedAt` è UTC: il consumer
non ha modo di saperlo dal payload.

- [ ] Aggiungere un campo `timezone` (es. `"Europe/Rome"`) alle risposte, o documentarlo esplicitamente in OpenAPI
- [ ] Coordinato con B3

### L6. Osservabilità minima
- [ ] Log strutturati coerenti (già ILogger, mancano negli scraper — vedi B2)
- [ ] Contatori base con `System.Diagnostics.Metrics`: scrape riusciti/falliti, durata, slot `Unknown` mappati
- [ ] Valutare OpenTelemetry se il servizio gira insieme ad altri

### L7. Miglioramenti API minori
- [ ] `Cache-Control`/`ETag` sulle risposte (gli agenti beneficiano di 304)
- [ ] Endpoint `GET /api/weather/{location}/today` come scorciatoia (comodo per agenti)
- [ ] Provider sconosciuto: oggi 404, più corretto 400 con lista dei provider validi nel messaggio (già presente nel testo dell'eccezione)
- [ ] Valutare versioning del path (`/api/v1/...`) prima che ci siano consumer esterni

---

## 🟢 Fase 4 — Qualità del codice e architettura

### Q1. Dispatcher basato su `dynamic`
`Dispatcher.cs:14-15,21-22`: binding a runtime, nessuna verifica a compile time, overhead DLR.

- [ ] Sostituire con wrapper generico compilato (pattern `HandlerWrapper<TQuery,TResult>` cacheato in `ConcurrentDictionary`) o valutare MediatR/Mediator source-generated

### Q2. Entità di dominio esposte direttamente dall'API
`GetForecastByDays` e `GetDayWeather` restituiscono `DayWeather` (entità Domain), mentre
week e next-24h hanno DTO dedicati. Incoerente e vincola il contratto API al dominio.

- [ ] DTO anche per i primi due endpoint (con `LastUpdatedAt`, coerente con B9)

### Q3. `WeatherType`: stringhe costanti invece di enum
`WeatherTypeEnums.cs` è una static class di costanti stringa (il nome file promette un enum).
Con un vero `enum` + `JsonStringEnumConverter` si otterrebbe type-safety mantenendo lo stesso
JSON. Nota: `HoursWeatherDetails.WeatherType` è `string` → il converter enum registrato in
`WeatherFileStorageJob.JsonOptions` oggi non ha alcun effetto.

- [ ] Convertire a `enum WeatherType` (payload JSON invariato)
- [ ] Correggere i typo `PressionMbar` → `PressureMbar` e "Athmospheric" → "Atmospheric" ⚠️ breaking per i consumer JSON (HA legge `pressionMbar`): pianificare con L7 (versioning) o mantenere alias

### Q4. Costanti magiche e piccoli refactor
- `ReliabilityPerc = 20` hardcoded per le pagine "complicated" (`3bMeteoScraper.cs:87`) → costante nominata con commento
- `WeatherProvider` è un wrapper di una stringa con setter pubblico → valutare record/value object
- Default `ReliabilityPerc = 100` quando il dato manca è discutibile (100 = massima fiducia): valutare `null` = "non disponibile"
- `Meteo3bScraper` scrapa sempre 8 giorni fissi (0-7) → costante configurabile

### Q5. Igiene repository
- [ ] `.editorconfig` con regole C# + `dotnet format` in CI
- [ ] Abilitare analyzers (`<AnalysisLevel>latest-recommended</AnalysisLevel>`) e `TreatWarningsAsErrors` dove sostenibile
- [ ] Uniformare i csproj (Domain/Application hanno `GenerateDocumentationFile`, Infrastructure no)

---

## 🔵 Fase 5 — Test

### T1. Coprire la logica più delicata, oggi non testata
I 25 test coprono solo il parsing HTML. Zero test su:
- **`GetNext24HoursForecastHandler`** — la logica finestra 24h + rollover mezzanotte è la più intricata del progetto (slot `18:00→00:00`, slot in corso, ordinamento)
- `GetWeekForecastHandler` (filtro `>= today`, `Take(7)`)
- `WeatherProviderResolver` (case-insensitive, provider mancante)
- `ParsePrecipitation`, `ExtractWindDirection`, `MapWeatherType` (direttamente, non via HTML)
- `WeatherFileStorageJob` (scrittura, cleanup cutoff)

### T2. Testabilità
- [ ] Iniettare `TimeProvider` negli handler/scraper (elimina la dipendenza da `DateTime.Now` e rende testabile la finestra 24h) — sinergia con B3
- [ ] Sostituire l'invocazione via reflection nei test (`Meteo3bScraperTests.cs:21-28`) con `internal` + `InternalsVisibleTo("Weather4Agents.Test")`
- [ ] Rimuovere la property duplicata `Complete1Html`/`V3Complete1Html` (stesso file)

### T3. CI
- [ ] Aggiungere al workflow: `dotnet list package --vulnerable --include-transitive` (fallisce su high) e coverage report (coverlet già referenziato ma non usato in pipeline)

---

## ⚪ Fase 6 — Documentazione e DX

- [ ] **README**: link rotto `docs/job.md` → `docs/jobs.md`; le istruzioni "Copy the .env.template" e `docker-compose up -d` non menzionano che i file stanno in `docker/`; typo "Yu can consume"
- [ ] Documentare il contratto API (timezone, significato di `reliabilityPerc`, comportamento con location sconosciuta) in una pagina `docs/api.md`
- [ ] `appsettings.Template.json` e `appsettings.json` sono identici: chiarire lo scopo del template o rimuoverlo
- [ ] `WeatherFileStorage:Enabled` e `CleanupEnabled` assenti da `appsettings.json`: aggiungerli espliciti (anche solo `false`) per scopribilità
- [ ] Nota etica/legale: lo scraping usa uno User-Agent da browser; verificare ToS/robots.txt di 3bmeteo e documentare la scelta dell'intervallo minimo consigliato
- [ ] HA integration: `manifest.json` — verificare `version` aggiornata a ogni release e documentare l'installazione via HACS se prevista

---

## Ordine di esecuzione consigliato

| Fase | Contenuto | Sforzo stimato | Rischio se rimandata |
|------|-----------|----------------|----------------------|
| 1 | S1 rotazione segreto, S2 bump pacchetto | Ore | Alto (segreto pubblico) |
| 2 | B1-B2-B4-B5-B8 (correttezza dati) poi B3-B9 (timezone/freshness), B6-B7, B10-B13 | 2-4 giorni | Medio-alto |
| 3 | S3 + L1-L2-L5 (contratto API robusto), poi L3-L4-L6-L7 | 2-3 giorni | Medio |
| 4 | Q1-Q5 | 1-2 giorni | Basso |
| 5 | T1-T3 (in parallelo alle fasi 2-3: ogni fix arriva col suo test) | continuo | Medio |
| 6 | Documentazione | Ore | Basso |

Note trasversali:
- B3 (timezone), B9 (freshness) e T2 (`TimeProvider`) convengono fatti insieme: toccano gli stessi punti
- Q3 (rinomina campi JSON) e L7 (versioning) vanno decisi insieme perché entrambi toccano il contratto pubblico consumato da Home Assistant e dagli agenti
