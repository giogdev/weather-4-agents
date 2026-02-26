# Weather4Agents
## Project objective
This project was created to build a middleware that enables agents to receive weather information. The data is scraped from the preferred provider and stored locally as JSON files. This way, agents consume fewer tokens to retrieve weather information.

# Guidelines
- Ask me if anything is unclear
- The project follows clean architecture paradigms
- The project follows .NET Core (>= 10.0) best practices
- Code and comments in English
- Before making significant updates, prepare a planning document in todo.md
- never commit changes

# Project features
- REST API: returns weather information based on the request (single day / N-day forecast)
- HybridCache to cache the most up-to-date weather information
- Saving forecasts to a file system directory as JSON files (organized by location, one JSON file per day). This aspect is managed by an automated task that runs every X minutes, where X is a configurable interval