# Docker

## Build

Build the image from the solution root:

```bash
docker build -t giogdev/weather4agents .
docker build -t giogdev/weather4agents:1.0.0 .
docker build -t giogdev/weather4agents:latest .
```

## Run

### Docker compose

Run with default configuration:
```bash
docker-compose up -d
```
> ⚠️ Remember to set up your .env file (from .env.template)

### Docker

Run with default configuration:

```bash
docker run -p 8080:8080 giogdev/weather4agents
```

Run with custom settings and persistent storage for weather data files:

```bash
docker run -p 8080:8080 \
  -e WeatherScraping__DefaultProvider=3bMeteo \
  -e WeatherScraping__EnabledProviders__0=3bMeteo \
  -e WeatherScraping__Locations__0=Bergamo \
  -e WeatherScraping__JobIntervalMinutes=60 \
  -e WeatherFileStorage__OutputPath=/data/weather \
  -e WeatherFileStorage__JobIntervalMinutes=60 \
  -v ./weather-data:/data/weather \
  giogdev/weather4agents
```
