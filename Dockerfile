FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Weather4Agents.API/Weather4Agents.API.csproj", "Weather4Agents.API/"]
COPY ["Weather4Agents.Application/Weather4Agents.Application.csproj", "Weather4Agents.Application/"]
COPY ["Weather4Agents.Domain/Weather4Agents.Domain.csproj", "Weather4Agents.Domain/"]
COPY ["Weather4Agents.Infrastructure/Weather4Agents.Infrastructure.csproj", "Weather4Agents.Infrastructure/"]

RUN dotnet restore "Weather4Agents.API/Weather4Agents.API.csproj"

COPY . .

RUN dotnet build "Weather4Agents.API/Weather4Agents.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Weather4Agents.API/Weather4Agents.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Weather4Agents.API.dll"]
