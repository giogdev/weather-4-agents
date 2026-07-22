FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
LABEL org.opencontainers.image.title="Weather4Agents" \
      org.opencontainers.image.description="Middleware that enables agents to receive weather information"
# curl is needed by the HEALTHCHECK below and is not present in the base image; install it as
# root before dropping to the non-root 'app' user.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app
EXPOSE 8080

# Report container health from the app's own /health endpoint. The start period gives the
# first scraping cycle time to run before failures count against the container.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Weather4Agents.API/Weather4Agents.API.csproj", "Weather4Agents.API/"]
COPY ["Weather4Agents.Application/Weather4Agents.Application.csproj", "Weather4Agents.Application/"]
COPY ["Weather4Agents.Domain/Weather4Agents.Domain.csproj", "Weather4Agents.Domain/"]
COPY ["Weather4Agents.Infrastructure/Weather4Agents.Infrastructure.csproj", "Weather4Agents.Infrastructure/"]

RUN dotnet restore "Weather4Agents.API/Weather4Agents.API.csproj"

COPY . .

RUN dotnet publish "Weather4Agents.API/Weather4Agents.API.csproj" \
    -c $BUILD_CONFIGURATION \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Weather4Agents.API.dll"]
