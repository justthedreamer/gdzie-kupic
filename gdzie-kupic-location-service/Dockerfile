# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first so restore is cached independently of source changes.
COPY src/GdzieKupicLocationService.API/GdzieKupicLocationService.API.csproj \
     GdzieKupicLocationService.API/

RUN dotnet restore GdzieKupicLocationService.API/GdzieKupicLocationService.API.csproj

# Copy remaining source and publish
COPY src/ .

RUN dotnet publish GdzieKupicLocationService.API/GdzieKupicLocationService.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ─── Runtime stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Use the built-in non-root user provided by the base image (UID/GID 1000)
COPY --from=build /app/publish .

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "GdzieKupicLocationService.API.dll"]
