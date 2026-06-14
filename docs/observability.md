# Observability

Covers structured logging, log enrichment, and how to query logs locally.
Metrics and distributed tracing are deferred post-MVP (see NFR-OBS-5).

---

## Stack

| Concern | Tool | Notes |
|---|---|---|
| Structured logging | [Serilog](https://serilog.net/) | All .NET services |
| Log aggregation | [Seq](https://datalust.co/seq) | Web UI at `http://localhost:5341` in local dev |
| Console output | `Serilog.Formatting.Compact` | Colored text in Development, compact JSON in containers |
| Error tracking | Seq (full stack traces) | Sentry deferred post-MVP |
| Health checks | `/health` endpoint | Each service, polled by Docker and uptime monitors |

---

## Logging Architecture

Every `.NET` service is configured with the same Serilog pipeline:

```
App code
  └─► Serilog logger (enriched)
        ├─► Console sink
        │     Development  → human-readable colored text
        │     Other        → CompactJsonFormatter (one JSON line per event)
        └─► Seq sink (http://seq:80 in containers, http://localhost:5341 locally)
```

### Bootstrap logger

Each `Program.cs` sets up a minimal bootstrap logger **before** configuration is loaded.
This ensures startup crashes (e.g. bad config, failed DI registration) are always written to stdout:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
```

The full logger (with Seq, enrichers, and level overrides) is wired via `builder.Host.UseSerilog(...)`.

---

## Log Enrichment

Every log event is enriched with the following properties:

| Property | Value | How |
|---|---|---|
| `ServiceName` | `GdzieKupicService` / `GdzieKupicLocationService` | `appsettings.json` constant via `Enrich.WithProperty` |
| `MachineName` | Container hostname (e.g. `gk-service`) | `Enrich.WithMachineName()` |
| `EnvironmentName` | `Development` / `Production` | `Enrich.WithEnvironmentName()` |
| `TraceId` | ASP.NET Core `Activity.TraceId` | Middleware, per-request |
| `CorrelationId` | `X-Correlation-Id` header, falls back to `TraceId` | Middleware, per-request |

`TraceId` and `CorrelationId` are pushed to `LogContext` by a thin middleware registered before `UseSerilogRequestLogging()`. This means **every** log line emitted anywhere during a request automatically carries both properties — including logs from services, repositories, and background handlers that don't know about HTTP at all.

---

## Request Logging

`app.UseSerilogRequestLogging()` replaces the verbose multi-line ASP.NET Core request logging with a single structured line per request:

```
[13:21:52 INF] HTTP GET /health responded 200 in 3.2 ms
```

Structured properties on this event: `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`.

`/health` requests are intentionally **not** filtered out — they are useful for diagnosing container health check failures.

---

## Configuration

Log levels are set in `appsettings.json` and can be changed without a rebuild:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning"
    }
  }
}
```

The Seq URL and API key come from environment variables (via `appsettings.json` defaults overridden by Docker env):

| Config key | Env variable | Default (local) |
|---|---|---|
| `Seq:Url` | `Seq__Url` | `http://localhost:5341` |
| `Seq:ApiKey` | `Seq__ApiKey` | *(empty — no auth)* |

---

## Hangfire Job Logging

> Implemented in Phase 1 `Gdzie.Kupic.Hangfire` scaffold.

Every job must log a structured entry at three points using the same `ILogger<T>` pattern:

```csharp
// Start
_logger.LogInformation("Job {JobId} starting for Post {PostId} CorrelationId {CorrelationId}", jobId, postId, correlationId);

// Completion
_logger.LogInformation("Job {JobId} completed for Post {PostId}", jobId, postId);

// Retry
_logger.LogWarning("Job {JobId} retrying (attempt {Attempt}) for Post {PostId}: {Error}", jobId, attempt, postId, error);
```

This satisfies **NFR-OBS-3**.

---

## Health Checks

Each service exposes `GET /health` returning `200 OK` with a JSON body:

```json
{ "status": "healthy", "service": "GdzieKupicService" }
```

Docker Compose polls this endpoint (see `healthcheck:` in `docker-compose.yaml`) and marks the container unhealthy if it fails, preventing dependent services from starting against an unhealthy backend.

> Extended health checks (database connectivity, MinIO reachability) will be added using `Microsoft.Extensions.Diagnostics.HealthChecks` in a later phase.

---

## Verifying Locally

### 1. Logs appear in Seq

```
1. docker compose up -d
2. GET http://localhost:5000/health
3. Open http://localhost:5341
4. Search: ServiceName = 'GdzieKupicService'
```

Expect: startup log lines + one request log line per HTTP call.

### 2. Required properties are present

Open any event in Seq and confirm: `ServiceName`, `MachineName`, `EnvironmentName`, `TraceId`, `CorrelationId`.

### 3. CorrelationId forwarding

```http
GET http://localhost:5000/health
X-Correlation-Id: my-trace-123
```

Search in Seq: `CorrelationId = 'my-trace-123'` — the request line must appear.

### 4. Structured request log

Search: `@MessageTemplate like 'HTTP%'` — each entry must have `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed` as first-class properties, not embedded in a string.

### 5. JSON output in containers

Set `ASPNETCORE_ENVIRONMENT=Production` in `.env`, rebuild (`docker compose up --build -d --no-deps gk-service`), then:

```powershell
docker compose logs gk-service
```

Each line must be a valid JSON object.
