# Local Development Environment

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose v2)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) â€” only needed when running services outside Docker

---

## Stack

| Container | Image | Purpose |
|---|---|---|
| `gk-postgres` | `postgis/postgis:16-3.4` | Primary database â€” PostgreSQL 16 with PostGIS 3.4 |
| `gk-minio` | `minio/minio:latest` | S3-compatible object storage for chat attachments |
| `gk-seq` | `datalust/seq:latest` | Structured log aggregation and search |
| `gk-service` | build `./gdzie-kupic-service` | Core backend (modular monolith) |

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
| `JWT_SECRET` | *(required)* | Token signing secret, â‰¥ 32 characters |
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

### Access the API documentation (Swagger UI)

Each .NET service exposes an OpenAPI document and an interactive Swagger UI when running in the
`Development` environment (the default for `docker compose up`):

- UI: `http://localhost:5000/swagger` (or the equivalent port for other services, see
  [Service Ports](#service-ports))
- Raw OpenAPI document: `http://localhost:5000/swagger/v1/swagger.json`

Use the "Authorize" button to paste a JWT access token (obtained from `/auth/sign-in` or
`/auth/sign-up`) so requests to protected endpoints are authenticated, then use "Try it out" on
any endpoint to send a real request directly from the browser â€” no separate REST client needed.

When running a service locally with `dotnet run` instead of Docker, the same paths are available
on whatever port `dotnet run` prints (see [launchSettings.json](../gdzie-kupic-service/src/Gdzie.Kupic.API/Properties/launchSettings.json), e.g. `http://localhost:5211/swagger`).

### Access MinIO console

Open [http://localhost:9001](http://localhost:9001) and log in with `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`.
The `attachments` bucket is created by the application on first startup.

### Connect to PostgreSQL

```bash
docker exec -it gk-postgres psql -U gdziekupic -d gdziekupic
```

Or use any PostgreSQL client pointed at `localhost:5432`.

### Run EF Core migrations

All commands are run from `gdzie-kupic-service/src`.

**Create a new migration** (after changing entities):

```powershell
dotnet ef migrations add <MigrationName> --project .\Gdzie.Kupic.Storage\ --startup-project .\Gdzie.Kupic.API\
```

**Apply migrations to the database:**

```powershell
dotnet ef database update --project .\Gdzie.Kupic.Storage\ --startup-project .\Gdzie.Kupic.API\
```

> The startup project needs to be running (or at least buildable with a valid connection string) for `database update`. Make sure the `gk-postgres` container is up first.

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
# Rebuild image and restart â€” skip restarting healthy dependencies
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

The locally running app connects to Seq on `http://localhost:5341` â€” exactly where Docker exposes it.
This works because `appsettings.json` defaults to `"Seq:Url": "http://localhost:5341"`, and the
`Seq__Url=http://seq:80` override in `docker-compose.yaml` only applies inside containers.

---

## Mock Accounts & Pre-Generated Tokens

> **This exists only because this project never runs in a production environment with real user
> data.** The same approach (long-lived, publicly documented JWTs and a fixed signing secret)
> would be a serious security defect in any production-bound project. See
> [design-decisions.md §12](../docs/design-decisions.md#12-admin) and issue #25.

Three fixed accounts are seeded idempotently at startup — one per role — so the API can be
exercised immediately without registering or logging in first. Each account also works normally
through `POST /auth/sign-in` with the email/password below.

| Role | Email | Password |
|---|---|---|
| Admin | value of ADMIN_EMAIL (.env) | value of ADMIN_PASSWORD (.env) |
| Buyer | `buyer-test@gdziekupic.local` | `Buyer123!` |
| Merchant | `merchant-test@gdziekupic.local` | `Merchant123!` |

Each account also has a static, pre-generated JWT access token with an effectively non-expiring
lifetime (expires in the year 2125). These are normal, fully-validated JWTs — subject to the same
authorization and account-status (banned/active) rules as any token issued through the API — not a
bypass mechanism. Paste one into the Swagger UI "Authorize" button (see
[Access the API documentation](#access-the-api-documentation-swagger-ui)) to call protected
endpoints immediately.

They are only valid when the API is signing/validating tokens with the fixed default
`JWT_SECRET` from `.env.example`. If you changed `JWT_SECRET` to a different value, these
tokens will be rejected — sign in normally instead to get a fresh token.

**Admin token:**
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDEiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsImp0aSI6IjMzODg5MTAyLTUzMWUtNDk1ZS05ZjU1LTdhOTlmODEwNjIwMCIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.n-dmUBeWB2P5DQfNMjJTXo0xwPCdmQWnbDFVM8o39Cs
```

**Buyer token:**
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDIiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJCdXllciIsImp0aSI6ImM3MTM1ZmVkLTM4NGQtNGRiOS05YmQ4LWJiNjM4MTE2MzJhZSIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.WeftJ8-x9_E-BcNEkNSANy3lv2Nm2U_jdmatbSccR9s
```

**Merchant token:**
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJNZXJjaGFudCIsImp0aSI6IjZmZWU4OWY2LTJhYzAtNDdkMy1hODNhLWMzMzBlZTcwNTczMCIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.KVkFwnZmHta80O6YhrnwR-kp6nhpdTTqKdwFJWhaV-Y
```

---

## Observability

For the full logging architecture, enrichment properties, and step-by-step verification guide see [observability.md](observability.md).

