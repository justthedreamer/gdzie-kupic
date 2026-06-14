# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first so restore is cached independently of
# source changes.
COPY src/GdzieKupicService.sln .
COPY src/GdzieKupicService.API/GdzieKupicService.API.csproj                         GdzieKupicService.API/
COPY src/GdzieKupicService.BLL/GdzieKupicService.BLL.csproj                         GdzieKupicService.BLL/
COPY src/GdzieKupicService.Contracts/GdzieKupicService.Contracts.csproj             GdzieKupicService.Contracts/
COPY src/GdzieKupicService.Infrastructure/GdzieKupicService.Infrastructure.csproj   GdzieKupicService.Infrastructure/

RUN dotnet restore GdzieKupicService.sln

# Copy remaining source and publish
COPY src/ .

RUN dotnet publish GdzieKupicService.API/GdzieKupicService.API.csproj \
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

ENTRYPOINT ["dotnet", "GdzieKupicService.API.dll"]
