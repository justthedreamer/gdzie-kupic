# gdzie-kupic-service

Core backend for the **Gdzie Kupic** reverse marketplace platform — a modular monolith built with ASP.NET Core, PostgreSQL + PostGIS, SignalR, and Hangfire.

## Repositories

| Repo | Role |
|---|---|
| `gdzie-kupic-service` ← *you are here* | Core backend — modular monolith |
| [`gdzie-kupic-ui`](https://github.com/justthedreamer/gdzie-kupic-ui) | PWA frontend (Vue.js + Vite) |
| [`gdzie-kupic-location-service`](https://github.com/justthedreamer/gdzie-kupic-location-service) | Geocoding service — stateless Google Maps proxy |

## Documentation

See [`docs/`](docs/index.md) for the full documentation index.

## Tech Stack

- **Runtime**: .NET 10 / ASP.NET Core
- **Database**: PostgreSQL + PostGIS
- **Real-time**: SignalR
- **Background jobs**: Hangfire
- **File storage**: S3-compatible (MinIO locally, AWS S3 in production)
- **Logging**: Serilog → Seq
- **Auth**: JWT + refresh token rotation

## Local Development

> See [docs/local-dev.md](docs/local-dev.md) — setup guide coming before the first dev sprint.

## Project Status

Design phase complete. Implementation planned in 9 phases — see [docs/planning.md](docs/planning.md).
