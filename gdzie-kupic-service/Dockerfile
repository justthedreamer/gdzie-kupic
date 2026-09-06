# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so restore is cached independently of source
# changes. Only Gdzie.Kupic.API and its direct/transitive project references
# are needed to build the runtime image.
COPY src/Gdzie.Kupic.API/Gdzie.Kupic.API.csproj                     Gdzie.Kupic.API/
COPY src/Gdzie.Kupic.API.Contract/Gdzie.Kupic.API.Contract.csproj   Gdzie.Kupic.API.Contract/
COPY src/Gdzie.Kupic.Auth/Gdzie.Kupic.Auth.csproj                   Gdzie.Kupic.Auth/
COPY src/Gdzie.Kupic.Domain/Gdzie.Kupic.Domain.csproj               Gdzie.Kupic.Domain/
COPY src/Gdzie.Kupic.Location/Gdzie.Kupic.Location.csproj           Gdzie.Kupic.Location/
COPY src/Gdzie.Kupic.Storage/Gdzie.Kupic.Storage.csproj             Gdzie.Kupic.Storage/

RUN dotnet restore Gdzie.Kupic.API/Gdzie.Kupic.API.csproj

# Copy remaining source and publish
COPY src/ .

RUN dotnet publish Gdzie.Kupic.API/Gdzie.Kupic.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ─── Runtime stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is required by the docker-compose healthcheck for this service.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Use the built-in non-root user provided by the base image (UID/GID 1000)
COPY --from=build /app/publish .

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "Gdzie.Kupic.API.dll"]
