# Local Development Environment

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose v2)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) — only needed when running services outside Docker

---

## Stack

| Container | Image | Purpose |
|---|---|---|
| `gk-postgres` | `postgis/postgis:16-3.4` | Primary database — PostgreSQL 16 with PostGIS 3.4 |
| `gk-minio` | `minio/minio:latest` | S3-compatible object storage for chat attachments |
| `gk-seq` | `datalust/seq:latest` | Structured log aggregation and search |
| `gk-service` | build `./gdzie-kupic-service` | Core backend (modular monolith) |
| `gk-location-service` | build `./gdzie-kupic-location-service` | Geocoding proxy over Google Maps |

---

## Getting Started

### 1. Copy and configure environment variables

```bash
cp .env.example .env
```

Edit `.env` and replace every placeholder value:

| Variable | What to change |
|---|---|
| `POSTGRES_PASSWORD` | Any strong password |
| `MINIO_ROOT_PASSWORD` | Any strong password |
| `JWT_SECRET` | Run `openssl rand -base64 32` and paste the result |
| `ADMIN_EMAIL` / `ADMIN_PASSWORD` | Credentials for the seeded Admin account |
| `GOOGLE_MAPS_API_KEY` | Your Google Maps Geocoding API key |

> **Never commit `.env` to source control.** It is listed in `.gitignore`.

### 2. Start infrastructure services

If the application Dockerfiles have not been created yet (early Phase 1), start only the infrastructure:

```bash
docker compose up postgres minio seq
```

### 3. Start the full stack

Once all Dockerfiles are present:

```bash
docker compose up --build
```

Add `-d` to run in detached mode:

```bash
docker compose up --build -d
```

### 4. Stop and clean up

```bash
# Stop containers, keep volumes
docker compose down

# Stop containers and remove all data volumes
docker compose down -v
```

---

## Service Ports

| Service | Host port | URL |
|---|---|---|
| PostgreSQL | `5432` | `postgresql://localhost:5432/gdziekupic` |
| MinIO S3 API | `9000` | `http://localhost:9000` |
| MinIO web console | `9001` | `http://localhost:9001` |
| Seq | `5341` | `http://localhost:5341` |
| gdzie-kupic-service | `5000` | `http://localhost:5000` |
| gdzie-kupic-location-service | `5001` | `http://localhost:5001` |

---

## Environment Variables

All variables are defined in `.env.example` with inline descriptions.
Copy to `.env` and fill in real values before starting the stack.

| Variable | Default | Description |
|---|---|---|
| `POSTGRES_USER` | `gdziekupic` | Database username |
| `POSTGRES_PASSWORD` | *(required)* | Database password |
| `POSTGRES_DB` | `gdziekupic` | Database name |
| `MINIO_ROOT_USER` | `minioadmin` | MinIO admin username |
| `MINIO_ROOT_PASSWORD` | *(required)* | MinIO admin password |
| `MINIO_BUCKET_NAME` | `attachments` | S3 bucket for file uploads |
| `SEQ_API_KEY` | *(empty)* | Seq ingestion API key; leave empty to accept all |
| `JWT_SECRET` | *(required)* | Token signing secret, ≥ 32 characters |
| `JWT_EXPIRY_DAYS` | `7` | Access token lifetime |
| `ADMIN_EMAIL` | `admin@gdziekupic.local` | Seeded admin account email |
| `ADMIN_PASSWORD` | *(required)* | Seeded admin account password |
| `GOOGLE_MAPS_API_KEY` | *(required)* | Google Maps Geocoding API key |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core runtime environment |

---

## Common Tasks

### Access Seq (structured logs)

Open [http://localhost:5341](http://localhost:5341) in your browser.
All .NET services ship structured logs via Serilog to this instance.

### Access MinIO console

Open [http://localhost:9001](http://localhost:9001) and log in with `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`.
The `attachments` bucket is created by the application on first startup.

### Connect to PostgreSQL

```bash
docker exec -it gk-postgres psql -U gdziekupic -d gdziekupic
```

Or use any PostgreSQL client pointed at `localhost:5432`.

### Run EF Core migrations

```bash
cd gdzie-kupic-service/src
dotnet ef database update --project GdzieKupicService.Infrastructure --startup-project GdzieKupicService.API
```

### Run the stack without blocking the terminal

```powershell
docker compose up -d
```

### View logs after starting detached

```powershell
# All services
docker compose logs -f

# Single service
docker compose logs -f gk-service
```

### Rebuild and restart a single service after a code change

```powershell
# Rebuild image and restart — skip restarting healthy dependencies
docker compose up -d --build --no-deps gk-service

# Location service
docker compose up -d --build --no-deps gk-location-service
```

### Rebuild a single service (image only, no restart)

```powershell
docker compose build gk-service
docker compose up -d --no-deps gk-service
```

### View logs for a service

```powershell
docker compose logs -f gk-service
```

### Run a service locally against Docker infrastructure

You can start only the infrastructure containers and run a .NET service directly on the host:

```powershell
# Start only what you need
docker compose up -d seq
docker compose up -d postgres minio seq

# Then run the service locally
cd gdzie-kupic-service/src/GdzieKupicService.API
dotnet run
```

The locally running app connects to Seq on `http://localhost:5341` — exactly where Docker exposes it.
This works because `appsettings.json` defaults to `"Seq:Url": "http://localhost:5341"`, and the
`Seq__Url=http://seq:80` override in `docker-compose.yaml` only applies inside containers.

---

## Observability

For the full logging architecture, enrichment properties, and step-by-step verification guide see [observability.md](observability.md).
