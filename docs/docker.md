# Docker

The compose file and the `.env.template` live in the [`docker/`](../docker/) directory; run the
commands below from there.

## Run

### Docker compose

Run with default configuration:
```bash
cd docker
cp .env.template .env   # first time only
docker compose up -d
```
> ⚠️ Remember to set up your `.env` file (from `.env.template`) before starting.

### Docker

Run with default configuration:

```bash
docker run -p 8080:8080 giogdev/weather4agents
```

Run with custom settings and persistent storage for weather data files. The container writes to
`/app/weather-data` by default, so mount a host directory there (or override `OutputPath`):

```bash
docker run -p 8080:8080 \
  -e WeatherScraping__DefaultProvider=3bMeteo \
  -e WeatherScraping__EnabledProviders__0=3bMeteo \
  -e WeatherScraping__Locations__0=Bergamo \
  -e WeatherScraping__JobIntervalMinutes=60 \
  -e WeatherFileStorage__Enabled=true \
  -v ./weather-data:/app/weather-data \
  giogdev/weather4agents
```

## Build

Build the image from the solution root:

```bash
docker build -t giogdev/weather4agents .
docker build -t giogdev/weather4agents:1.0.0 .
docker build -t giogdev/weather4agents:latest .
```