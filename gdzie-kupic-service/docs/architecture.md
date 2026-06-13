# System Architecture

This document is the authoritative architecture reference for the Gdzie Kupic platform.
It defines service boundaries, responsibilities, communication patterns, data ownership, and infrastructure decisions.

## Repositories

| Repository | Role |
|---|---|
| `gdzie-kupic-service` | Core backend — modular monolith |
| `gdzie-kupic-ui` | PWA frontend |
| `gdzie-kupic-location-service` | Geocoding service |

---

## 1. Architecture Style

- The core backend (`gdzie-kupic-service`) is a **modular monolith**: a single deployable unit with a `Gdzie.Kupic.Marketplace` assembly (Posts, Merchants, Matching, Catalogue, Admin subfolders) alongside dedicated modules for Auth, Chat, Notifications, Realtime, Location, Storage, and Hangfire
- Each module owns its own data access layer and exposes no direct internal dependencies to other modules
- This structure preserves the option to extract a module into an independent service if scaling demands it, without requiring a rewrite
- `gdzie-kupic-location-service` is a standalone stateless service: it has no shared database, no transactional coupling with the core, and acts purely as a geocoding proxy over the Google Maps API
- `gdzie-kupic-ui` is an independently deployed PWA

---

## 2. Service Responsibilities

### `gdzie-kupic-location-service` — Geocoding Service

- Accepts an address string and returns coordinates (latitude, longitude, display name)
- Proxies requests to the Google Maps Geocoding API
- Stateless — no database, no persistent state
- Called only when a user saves a location (account setup, adding/editing a saved location); never called during post creation or merchant matching

### `gdzie-kupic-ui` — PWA Frontend

- Buyer and merchant-facing Progressive Web App
- Communicates exclusively with `gdzie-kupic-service` (REST + SignalR)
- Calls `gdzie-kupic-location-service` indirectly via `gdzie-kupic-service` — the UI never calls the location service directly

### `gdzie-kupic-service` — Core Backend Modules

| Module | Responsibilities |
|---|---|
| `Gdzie.Kupic.API` | Application entry point — HTTP controllers, middleware, DI composition root |
| `Gdzie.Kupic.API.Contracts` | Public API definitions — request/response DTOs, shared across API and test projects |
| `Gdzie.Kupic.Realtime` | SignalR hubs and hub registration infrastructure; implements all module push interfaces (`IPostFeedChannel`, `IChatChannel`, `INotificationChannel`) using `IHubContext<T>`; the only module with a SignalR dependency |
| `Gdzie.Kupic.Auth` | Registration, login, JWT issuance, refresh token rotation, password reset, account status enforcement |
| `Gdzie.Kupic.Marketplace` | Single assembly containing all core domain logic, organised into subfolders with internal boundaries: |
| &nbsp;&nbsp;`/Posts` | Post creation, lifecycle management (`Active → Fulfilled / Closed / Expired`), expiry scheduling, urgency handling, outbox entry creation; defines `IPostFeedChannel` |
| &nbsp;&nbsp;`/Merchants` | `Merchants` (business entity), `MerchantAccounts` (User↔Merchant link), `MerchantBranches` (physical locations with coordinates and contact details), category/tag subscriptions, response state machine |
| &nbsp;&nbsp;`/Matching` | Merchant matching on post creation (PostGIS spatial query), new merchant post scanning, outbox relay |
| &nbsp;&nbsp;`/Catalogue` | Category and tag taxonomy — create, rename, soft-disable; read access for post creation and merchant subscriptions |
| &nbsp;&nbsp;`/Admin` | User ban enforcement (delegates to `Auth`), taxonomy management (internal to `Catalogue` subfolder) |
| `Gdzie.Kupic.Chat` | Message persistence, thread management, image attachment references; defines `IChatChannel` |
| `Gdzie.Kupic.Notifications` | Push subscription management, deduplication, Web Push and email dispatch; defines `INotificationChannel` |
| `Gdzie.Kupic.Location` | HTTP client wrapping `gdzie-kupic-location-service` — address → coordinates conversion; called only during location save |
| `Gdzie.Kupic.Storage` | Public API for file storage — upload, retrieve, delete; abstracts the S3-compatible backend |
| `Gdzie.Kupic.Hangfire` | Hangfire abstraction — exposes a public interface for enqueuing and scheduling jobs; other modules never reference Hangfire directly |

**Test projects:**

| Project | Scope |
|---|---|
| `Gdzie.Kupic.Tests.Unit` | Pure unit tests — no database, no I/O, no network |
| `Gdzie.Kupic.Tests.Functional` | Module-level tests with a real database (Testcontainers) — tests slices in isolation |
| `Gdzie.Kupic.Tests.System` | End-to-end tests against a running application instance |

**Module dependency rules:**

- `API` → `Realtime`, all feature modules
- `Realtime` → `Marketplace`, `Chat`, `Notifications` (implements their channel interfaces using SignalR; the only module that references `Microsoft.AspNetCore.SignalR`)
- `Marketplace` → `Location`, `Notifications`, `Hangfire`, `Auth` (Admin subfolder only)
- `Notifications` → `Hangfire`; defines `INotificationChannel`
- `Chat` → `Storage`, `Hangfire`; defines `IChatChannel`
- `Location` → external HTTP client only (no module dependencies)
- `Storage` → no module dependencies
- `Hangfire` → no module dependencies
- `Auth` → no module dependencies
- No module may depend on `API`, `API.Contracts`, or `Realtime` (dependency flows inward only)

---

## 3. Inter-Service Communication

- `gdzie-kupic-ui` → `gdzie-kupic-service`: REST over HTTPS for all data operations; SignalR WebSocket for real-time events (status panel, feed updates, chat)
- `gdzie-kupic-service` → `gdzie-kupic-location-service`: synchronous HTTP REST call only during location save (account setup, adding/editing a saved location); never called during post creation or matching
- `gdzie-kupic-service` → Google Maps Geocoding API: proxied through `gdzie-kupic-location-service`; never called directly from the core
- No message broker or event bus for MVP — all inter-module communication within `gdzie-kupic-service` is in-process

**Post-MVP consideration:** `Gdzie.Kupic.Matching` is the primary candidate for extraction into an independent service if subscription scoring, relevance ranking, or multiple merchant locations are introduced. The module boundary already isolates it for this purpose.

---

## 4. Data Architecture

- **Primary database**: PostgreSQL with PostGIS extension — used by `gdzie-kupic-service` for all persistent data
- **Spatial data**: `MerchantBranches.Coordinates` stored as `GEOGRAPHY(Point, 4326)`; spatial index required for radius match queries on post creation
- **Buyer saved locations**: stored in PostgreSQL as a collection of named coordinate points per buyer account; geocoded once on save, reused on post creation
- **Background jobs**: Hangfire job store persisted to the same PostgreSQL instance
- **Outbox table**: `Outbox` records are written in the same transaction as the triggering domain write; processed by `OutboxRelayJob` and never deleted — retained for audit
- **File storage**: chat image attachments stored in an S3-compatible object store; application code uses the S3 API exclusively — storage backend is swappable via configuration
- `gdzie-kupic-location-service` has no database of its own

---

## 5. Authentication & Authorization

- JWT-based: access token (default 7 days, configurable) + refresh token stored in the database
- Refresh tokens are rotated on every use; previous token is invalidated immediately
- Account status (active / banned) is checked on every authenticated request; result is short-lived cached (~1 minute) to limit database load
- Banned accounts are rejected immediately regardless of token validity
- Three roles: `Buyer`, `MerchantAccount`, `Admin` — enforced at the API layer; no anonymous access
- `gdzie-kupic-location-service` is internal and not exposed to the public internet — no auth required between services for MVP

---

## 6. Real-Time Communication

- Transport: SignalR — single persistent connection per client, shared for all real-time events
- **Status panel** (buyers): client joins a channel scoped to their post; server pushes response state count updates
- **Merchant feed** (merchants): server pushes new matching post events and post removal events (closed / fulfilled / expired) to connected merchants in range
- **Chat**: server pushes new message events to both parties on a per-thread channel
- All SignalR updates are derived from persisted state — the database is the source of truth; SignalR is delivery only
- MVP targets a single application instance; no distributed SignalR backplane (e.g. Redis) required

**Per-module channel interfaces:**
- Each module that needs to push real-time events defines its own channel interface as part of its public contract — no SignalR dependency
- `Gdzie.Kupic.Marketplace` (`/Posts` subfolder) defines `IPostFeedChannel` — feed updates to merchants (`PostAdded`, `PostRemoved`)
- `Gdzie.Kupic.Chat` defines `IChatChannel` — new message events to thread participants
- `Gdzie.Kupic.Notifications` defines `INotificationChannel` — in-app notification events to buyers and merchants
- `Gdzie.Kupic.Realtime` implements all three interfaces as SignalR hubs and is the only module with a SignalR reference

**Module registration pattern:**
- `Realtime` exposes a `RealtimeBuilder` with `AddHub<TInterface, THub>()` — the constraint `where THub : Hub, TInterface` enforces at compile time that the hub implements the channel interface
- Each `AddHub` call produces exactly one DI registration (`TInterface → THub` as singleton)
- The full hub manifest lives in `Program.cs` — explicit, readable, no implicit wiring:

```csharp
builder.Services
    .AddRealtimeModule(realtime => realtime
        .AddHub<IPostFeedChannel, PostFeedHub>()
        .AddHub<IChatChannel, ChatHub>()
        .AddHub<INotificationChannel, NotificationHub>())
    .AddMarketplaceModule(config)
    .AddChatModule(config)
    .AddNotificationsModule(config);

app.MapRealtimeHubs();
```

---

## 7. Background Jobs

- **Runtime**: Hangfire, hosted in-process within `gdzie-kupic-service`
- **Job store**: PostgreSQL (same instance as application data)

**Outbox pattern for reliable job dispatch:**
- Post creation writes a `Post` record and an `Outbox` entry (`type: NotifyMerchants`, `payload: postId`) in a single database transaction
- An outbox relay job (scheduled every ~5 seconds via Hangfire) picks up unprocessed outbox entries, enqueues the corresponding fan-out job, and marks the entry as processed
- This guarantees that a persisted post always results in merchant notification dispatch, even if the application crashes between post creation and job enqueue
- Fan-out jobs are idempotent — safe to retry on failure

**Job inventory:**

| Job | Trigger | Description |
|---|---|---|
| `OutboxRelayJob` | Scheduled (~5s interval) | Picks up unprocessed outbox entries and enqueues downstream jobs |
| `NotifyMerchantsJob` | Enqueued by outbox relay on post creation | Queries matched merchants via PostGIS, writes `PostNotification` records, dispatches Web Push / email, updates post `NotificationDispatchStatus` to `Dispatched` |
| `NotifyNewMerchantJob` | Enqueued on merchant registration completion | Scans active posts matching the new merchant's location and subscriptions; dispatches notifications for matches not already in `PostNotification` |
| `ExpirePostsJob` | Scheduled (periodic) | Transitions posts past their expiry deadline to `Expired` state |
| `CleanPushSubscriptionsJob` | Triggered on delivery failure | Removes invalid or expired push subscription endpoints |

**Deduplication:**
- `PostNotification` has a unique constraint on `(PostId, MerchantId)`
- All notification dispatch jobs use `INSERT ... ON CONFLICT DO NOTHING` — retries and the new-merchant job are both naturally idempotent
- A merchant can never receive two push notifications for the same post

**Post notification dispatch status:**
- `Post` carries a `NotificationDispatchStatus` field: `Pending → Dispatched`
- The buyer status panel shows "Looking for merchants in your area..." while status is `Pending`
- Once `NotifyMerchantsJob` completes, status transitions to `Dispatched` and the panel switches to "Notified X merchants" (or zero-match popup if count is 0)

**Status panel counts (derived, never cached in memory):**

| Count | Source |
|---|---|
| Notified | `COUNT(PostNotification WHERE PostId = ?)` |
| Checking | Notified merchants with no `MerchantResponse` record |
| Has it / May have it / Can order it / Can't help | `MerchantResponse` grouped by state |

**Observability:**
- Every job logs a structured entry with `postId` and `correlationId` at start, completion, and each retry
- Hangfire's built-in job history (enqueued → processing → succeeded / failed + retry log) serves as the primary audit trail for dispatch debugging
- Event sourcing deferred post-MVP

**Fan-out scalability note:**
- A single post matching a large number of merchants could produce a long-running job holding a worker thread
- Mitigation: the parent `NotifyMerchantsJob` enqueues N child jobs in batches (e.g. 50 merchants per batch); each child job is independently retried on failure
- Worker count is configurable (default: `ProcessorCount * 5`); job queue depth is unlimited — jobs persist in PostgreSQL until processed
- Polling interval is configurable (default 15s); set to 1–2s for near-real-time notification dispatch

---

## 8. File Storage

- Chat image attachments are stored in an S3-compatible object store
- The application references files by key/URL; it never stores binary data in PostgreSQL
- **Local development**: MinIO running as a Docker container, accessed via the same S3 API as production
- **Production**: AWS S3 or equivalent; storage backend is selected via environment configuration — no code changes required to switch
- Maximum attachment size is configurable (default 5 MB)

---

## 9. Local Development Environment

- The full development environment runs entirely in Docker — both infrastructure and application services
- All services have a `Dockerfile`; a single `docker-compose.yml` at the monorepo root defines the complete local stack
- `gdzie-kupic-ui` is a Vue.js + Vite PWA; in development it runs the Vite dev server inside Docker with hot-module replacement
- Environment-specific configuration is managed via `.env` files; secrets are never committed

> Full setup documentation (ports, container names, environment variables, common tasks): [local-dev.md](local-dev.md)

---

## 10. Deployment

- MVP targets a single server deployment — no container orchestration required
- Each service is deployed as a Docker container; a `docker-compose.yml` (production variant) defines the full stack
- `gdzie-kupic-ui` is built with `vite build`; the output static files are served from an Nginx container
- Nginx acts as a reverse proxy: routes `/api` and SignalR (`/hubs`) traffic to `gdzie-kupic-service`, serves `gdzie-kupic-ui` static assets, and routes geocoding calls to `gdzie-kupic-location-service`
- TLS termination at Nginx
- `gdzie-kupic-location-service` is not exposed publicly — accessible only to `gdzie-kupic-service` on the internal Docker network

---

## 11. Observability

- **Logging**: Serilog in all .NET services writes structured JSON logs to stdout; Seq collects and indexes them
- **Seq**: runs as a Docker container in both local dev and production; provides a web UI at port 5341 for log search, filtering, and alerting; free for single-user use
- Every log entry is enriched with `serviceName`, `correlationId`, and `traceId` at minimum
- Every Hangfire job logs a structured entry with `jobId`, `postId`, and `correlationId` at start, completion, and each retry
- **Health checks**: each service exposes a `/health` endpoint polled by Nginx / an uptime monitor
- **Error tracking**: unhandled exceptions are logged to Seq with full stack traces; Sentry integration deferred post-MVP
- **Metrics**: deferred post-MVP

---

## Open Questions (unresolved)
