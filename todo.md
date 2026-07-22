# Ticket 19 — Documentation & repository hygiene

## Goal
A new user can start the stack on the first try and integrators can rely on a documented
contract. Fix README/docker docs, add an API contract page, tidy the redundant settings
template, document ethical scraping, and add repository hygiene (`.editorconfig`, analyzers,
uniform project files, CI `dotnet format` + coverage).

Plan items covered: phase 6 (docs & DX), Q5 (repo hygiene), remainder of T3 (CI format + coverage;
the vulnerable-package check is already wired).

## Acceptance criteria (from the ticket)
- [ ] Every README link resolves; docker instructions match the real file locations
- [ ] The API contract page documents timezone, reliability, freshness and error behaviour as implemented
- [ ] `.editorconfig` and analyzers are active; the solution builds warning-clean or with documented suppressions
- [ ] CI runs format check and publishes coverage

## Findings (current state)
- README line 9: broken link `docs/job.md` → should be `docs/jobs.md`.
- README line 64: typo "Yu can consume".
- README getting-started (lines 11-16) says `docker-compose up -d` / copy `.env.template` but does
  not mention both files live under `docker/`.
- README env table lists a stale `WeatherFileStorage__JobIntervalMinutes` (jobs were merged in
  ticket 15 — there is no separate storage schedule) and omits newer knobs
  (`AllowUnconfiguredLocations`, `HttpTimeoutSeconds`, `RateLimiting__*`, `HealthCheck__MaxScrapeAgeMinutes`).
- `docs/jobs.md` still documents a separate `WeatherFileStorageJob` and its `JobIntervalMinutes`
  that no longer exist (one `WeatherScrapingJob` now does scrape → persist in a single cycle).
- `docs/docker.md` `docker run` example mounts `/data/weather` while the compose default and
  container path is `/app/weather-data` — inconsistent.
- `appsettings.Template.json` is redundant with `appsettings.json` and now stale (missing the
  `HealthCheck` section, carries the obsolete `WeatherFileStorage:JobIntervalMinutes`). Only
  reference is the plan doc → safe to remove.
- `.env.template` carries the obsolete `WeatherFileStorage__JobIntervalMinutes`.
- No `.editorconfig`; no analyzers enabled; project files diverge (Infrastructure/Test lack
  `GenerateDocumentationFile`; each production csproj repeats the same property block).
- CI (`.github/workflows/pipeline.yml`) has the vulnerable-package check but no `dotnet format`
  verify and no coverage collection/publish.

## Plan

### A. Documentation (phase 6)
1. **README** — fix `docs/job.md` → `docs/jobs.md`; fix "Yu can consume"; clarify that compose +
   `.env.template` live under `docker/`; correct the env-var table (drop stale storage interval,
   add the new knobs); link the new API and scraping docs.
2. **`docs/api.md`** (new) — endpoints, path/query params, `location` rules, `numberOfDays` bounds,
   timezone semantics (IANA `Timezone` field, all times provider-local), `reliabilityPerc` meaning
   (day-level 0-100, default 100, degraded pages = 20), freshness (`LastUpdatedAt` = scrape time in
   UTC, not response time), `ETag`/`Cache-Control`/`304`, and error behaviour (404 unknown location,
   400 validation, 403 whitelist, 429 rate limit).
3. **`docs/jobs.md`** — rewrite to the single merged job (bootstrap-from-disk → scrape → persist),
   remove the phantom `WeatherFileStorageJob` and `WeatherFileStorage:JobIntervalMinutes`.
4. **`docs/scraping.md`** (new) — ethical/legal note: browser User-Agent choice, honour ToS/robots,
   recommended minimum `JobIntervalMinutes` toward the provider; link from README.
5. **`appsettings.Template.json`** — remove (redundant + stale); README/`docs/*` document config.
6. **`docker/.env.template`** — remove the obsolete `WeatherFileStorage__JobIntervalMinutes`.
7. **`docs/docker.md`** — align the `docker run` volume/`OutputPath` with `/app/weather-data`.
8. **CHANGELOG.md** — add a docs & repo-hygiene entry.

### B. Repository hygiene (Q5)
9. **`.editorconfig`** — C# conventions (indentation, `var`, expression-bodied members, `using`
   ordering, file-scoped namespaces, naming). Style rules kept at `suggestion` severity so the
   build stays warning-clean; correctness analyzers do the enforcing.
10. **`Directory.Build.props`** (new, root) — centralise shared properties (`net10.0`, nullable,
    implicit usings, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`,
    `GenerateDocumentationFile` + `NoWarn 1591`); trim the now-duplicated blocks from each csproj so
    the project files are uniform. Test project opts out of doc generation.
11. Build warning-clean; document any unavoidable suppressions in the props/editorconfig.

### C. CI (remainder of T3)
12. **`.github/workflows/pipeline.yml`** — add `dotnet format --verify-no-changes` and collect
    coverage (`--collect:"XPlat Code Coverage"`), publishing the report as a build artifact.

## Verification
- `dotnet build` warning-clean; `dotnet format --verify-no-changes` passes.
- Full test suite green (baseline: 205 tests).
- Every relative link in README/docs resolves to a real file.
- `/code-review`, then commit.

## Checklist
- [x] A. Docs (README, api.md, jobs.md, scraping.md, template removal, .env.template, docker.md, CHANGELOG)
- [x] B. .editorconfig + Directory.Build.props + uniform csproj, warning-clean
- [x] C. CI format check + coverage
- [x] Build + format + full suite green (205 tests)
- [x] Code review (two-axis: standards + spec). Applied: stripped BOM from the two csproj files for
      editorconfig self-consistency; scoped CA1051/CA1725 suppressions to Infrastructure (active
      elsewhere); added `RateLimiting__QueueLimit` to the README env table. No spec contradictions.
- [ ] Commit
