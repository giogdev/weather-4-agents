# Piano: TTL di cache differenziato (oggi 30 min / giorni successivi 12 h)

## Obiettivo
Sostituire l'attuale cache monolitica (una entry per `{provider}:{location}` con TTL unico di 24 h)
con **due entry a vita indipendente** per località:

| Segmento | Contenuto | TTL | Pagine scrapate |
|---|---|---|---|
| **today** | solo il giorno corrente (provider-local) | **30 min** | `/{location}` (day offset 0) |
| **extended** | giorni 1..7 | **12 h** | `/{location}/{1..7}` |
| negativo (vuoto) | — | 5 min (invariato) | — |

Motivazione: presso il provider i dati di oggi si aggiornano ~ogni 30 min, quelli dei giorni
successivi cambiano lentamente. Poiché **ogni giorno è già una fetch HTTP indipendente**
([3bMeteoScraper.ScrapeAsync](Weather4Agents.Infrastructure/Scrapers/3bMeteoScraper.cs)), un miss
sul segmento *today* rifà **1 sola pagina**, non 8 → resta coerente con lo scraping responsabile.

## Stato attuale (per riferimento)
- Una entry `{provider}:{location}` → intero `ScrapedForecast`, TTL 24 h reale / 5 min negativo
  ([BaseWeatherScraper](Weather4Agents.Infrastructure/Scrapers/Base/BaseWeatherScraper.cs)).
- Default `AddHybridCache` = 24 h ([DependencyInjection.cs:76-80](Weather4Agents.Infrastructure/DependencyInjection.cs#L76-L80)).
- Tutti gli handler leggono `GetForecastAsync` e filtrano i giorni → **restano invariati**.
- Freshness: unico `ScrapedForecast.ScrapedAt`; ETag da `LastUpdatedAt.UtcTicks`.

## Decisione di design sulla freshness (RACCOMANDATA: opzione 1)
La forecast composta attinge da due scrape con tempi diversi.
- **Opz. 1 (raccomandata, KISS):** `ScrapedForecast.ScrapedAt = max(ScrapedAt dei segmenti che
  hanno prodotto giorni)`. Nessuna modifica al dominio. Effetto collaterale minore: un poller su
  `date/{giorno-futuro}` vede l'ETag ruotare ogni 30 min (quando *today* viene ri-scrapato) anche
  se quel giorno non è cambiato → qualche `200` in più invece di `304`. Endpoint `today`/`week`
  invariati e corretti.
- **Opz. 2 (più precisa, più invasiva):** `ScrapedAt` per-giorno su `DayWeather`; ogni handler
  calcola `LastUpdatedAt = max` sui giorni effettivamente restituiti. ETag ottimale per ogni
  endpoint. Tocca `DayWeather` (dominio), tutti i DTO di risposta, `IFreshnessStamped`, e il
  round-trip su file. Rimandabile a un ticket separato.

> **DECISO: opzione 1** (max globale, KISS). Nessuna modifica al dominio `DayWeather`.

## Intervento tecnico

### 1. Configurazione (nuove impostazioni)
- Estendere [WeatherScrapingSettings](Weather4Agents.Application/Settings/WeatherScrapingSettings.cs)
  con: `TodayCacheMinutes` (default 30, range 1..1440), `ExtendedCacheHours` (default 12,
  range 1..168), `NegativeCacheMinutes` (default 5, range 1..60). Validazione DataAnnotations.
- Aggiornare [appsettings.json](Weather4Agents.API/appsettings.json) sezione `WeatherScraping`.

### 2. Scraper: scrape per intervallo di giorni
- Cambiare l'astratto in `BaseWeatherScraper`:
  `ScrapeAsync(string location, int fromDayOffset, int toDayOffset, CancellationToken ct)`.
- [3bMeteoScraper](Weather4Agents.Infrastructure/Scrapers/3bMeteoScraper.cs): parametrizzare il
  loop `dayOffset` con `from..to` (oggi il loop è `0..MaxDays`).

### 3. BaseWeatherScraper: due entry + composizione
- Chiavi: `{provider}:{location}:today` e `{provider}:{location}:extended`
  (helper `CacheKeyFor(..., segment)`).
- Iniettare `IOptions<WeatherScrapingSettings>`; costruire `HybridCacheEntryOptions` per today
  (30 min) ed extended (12 h); mantenere `NegativeCacheOptions` (ora configurabile).
- Estrarre un helper privato `GetOrScrapeSegmentAsync(cacheKey, from, to, positiveOptions, ...)`
  che replica il pattern attuale "default negativo → promozione a TTL positivo se `Days>0`"
  (righe 70-82) una volta per segmento.
- `GetForecastAsync` compone: `Days = (today.Days ∪ extended.Days).OrderBy(Date)`;
  `ScrapedAt = max` sui segmenti con giorni (opz. 1).
- `forceRefresh: true` (usato dal job) rinfresca **entrambi** i segmenti.
- `SeedAsync` (bootstrap da disco): splittare i giorni letti in today/extended confrontando
  `d.Date` con `this.GetLocalToday(TimeProvider)`; stampare ciascuna entry con lo stamp da disco.

### 4. DependencyInjection
- `AddHybridCache` DefaultEntryOptions → allineare a 12 h (fallback; le entry meteo passano sempre
  opzioni esplicite) — [DependencyInjection.cs:74-81](Weather4Agents.Infrastructure/DependencyInjection.cs#L74).

### 5. Test
- [BaseWeatherScraperCachingTests](Weather4Agents.Test/Scrapers/BaseWeatherScraperCachingTests.cs):
  - Ora ci sono **2 entry** → sostituire gli `Assert.Single(l2.LastExpiration)`.
  - Nuovi assert TTL: today = 30 min, extended = 12 h, vuoto = 5 min.
  - `CountingScraper`: tracciare gli offset richiesti per verificare la **granularità**
    (miss di today ⇒ scrape solo offset 0; extended ancora servito da cache).
  - Nuovo test: scaduto today (30 min) → ri-scrape solo di oggi; extended non ri-scrapato.
- [Meteo3bScraperScrapeTests](Weather4Agents.Test/Scrapers/Meteo3bScraperScrapeTests.cs):
  adeguare alla firma `ScrapeAsync(location, from, to, ct)`.
- Verificare che passino ancora: `DataFreshnessTests`, `ApiConveniencesTests` (ETag/304),
  `BaseWeatherScraperFreshnessTests`.

## Aggiornamento documentazione
- [docs/scraping.md](docs/scraping.md) righe 18-32: riscrivere il punto "cached for 24 hours"
  con la politica differenziata (today 30 min, giorni successivi 12 h, vuoto 5 min) e spiegare che
  today si auto-rinfresca on-demand fra i cicli del job fetchando una sola pagina.
- [docs/api.md](docs/api.md) sez. "Freshness" / "Caching & conditional requests": chiarire che
  `lastUpdatedAt` di oggi è ≤ 30 min, dei giorni successivi ≤ 12 h; nota su rotazione ETag
  (secondo l'opzione scelta).
- Verificare [CLAUDE.md](CLAUDE.md) (feature "HybridCache") — eventuale nota sui nuovi setting.

## Fuori scope
- Opzione 2 (freshness per-giorno) salvo richiesta esplicita.
- `never commit` — commit solo su richiesta (branch `improvements` autorizzato).

## Esito implementazione
Raffinamento del design (blocco 3): invece di due scrape indipendenti per segmento, un **miss a
freddo esegue un unico scrape dell'intera settimana** (offset 0..7) che popola entrambi i segmenti
con lo stesso `ScrapedAt`; solo alla scadenza autonoma del segmento *today* (30 min) viene
ri-scrapata **la sola pagina di oggi** (offset 0..0). Questo mantiene "1 richiesta a freddo = 1
scrape" (contratto intuitivo, meno churn sui test) e un `ScrapedAt` coerente sul percorso comune.
Alla scadenza del segmento *extended* (12 h) l'intera settimana viene ri-scrapata.

### Checklist
- [x] Settings (`TodayCacheMinutes`/`ExtendedCacheHours`/`NegativeCacheMinutes`) + appsettings.json
- [x] `ScrapeAsync` per-intervallo (from..to) in Base + 3bMeteo
- [x] Doppia entry + composizione + `SeedAsync` split in `BaseWeatherScraper`
- [x] DI default HybridCache allineato (12 h)
- [x] Test: unit caching riscritti (granularità today-only, scadenza extended, TTL configurabili) +
      test-double adeguati (Fake offset-aware, costruttori). Suite verde: **208 test**
- [x] `dotnet format --verify-no-changes` pulito
- [x] Docs: scraping.md, api.md (Freshness + 404), README env table, CHANGELOG, docker/.env.template
- [ ] Commit (solo su richiesta)
