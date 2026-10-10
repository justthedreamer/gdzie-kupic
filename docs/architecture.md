# System Architecture

This document is the authoritative architecture reference for the Gdzie Kupic platform.
It defines service boundaries, responsibilities, communication patterns, data ownership, and infrastructure decisions.

## Repositories

| Repository | Role |
|---|---|
| `gdzie-kupic-service` | Core backend — modular monolith |
| `gdzie-kupic-ui` | PWA frontend |

---

## 1. Architecture Style

- The core backend (`gdzie-kupic-service`) is a **modular monolith**: a single deployable unit with a `Gdzie.Kupic.Marketplace` assembly (Posts, Merchants, Matching, Catalogue, Admin subfolders) alongside dedicated modules for Auth, Chat, Notifications, Realtime, Location, Storage, and Hangfire
- Each module owns its own data access layer and exposes no direct internal dependencies to other modules
- This structure preserves the option to extract a module into an independent service if scaling demands it, without requiring a rewrite
- `gdzie-kupic-ui` is an independently deployed PWA

---

## 2. Service Responsibilities

### `gdzie-kupic-ui` — PWA Frontend

- Buyer and merchant-facing Progressive Web App
- Communicates exclusively with `gdzie-kupic-service` (REST + SignalR)

### `gdzie-kupic-service` — Core Backend Modules

| Module | Responsibilities |
|---|---|
| `Gdzie.Kupic.API` | Application entry point — HTTP controllers, middleware, DI composition root |
| `Gdzie.Kupic.API.Contracts` | Public API definitions — request/response DTOs, shared across API and test projects |
| `Gdzie.Kupic.Realtime` | SignalR hubs and hub registration infrastructure; implements all module push interfaces (`IPostFeedChannel`, `IChatChannel`, `INotificationChannel`) using `IHubContext<T>`; the only module with a SignalR dependency |
| `Gdzie.Kupic.Auth` | Registration, login (password + Google OAuth), JWT issuance, refresh token rotation, password reset, account status enforcement |
| `Gdzie.Kupic.Marketplace` | Single assembly containing all core domain logic, organised into subfolders with internal boundaries: |
| &nbsp;&nbsp;`/Posts` | Post creation, lifecycle management (`Active → Fulfilled / Closed / Expired`), expiry scheduling, urgency handling, outbox entry creation; defines `IPostFeedChannel` |
| &nbsp;&nbsp;`/Merchants` | `Merchants` (business entity), `MerchantAccounts` (User↔Merchant link), `MerchantBranches` (physical locations with coordinates and contact details), category/tag subscriptions, response state machine |
| &nbsp;&nbsp;`/Matching` | Merchant matching on post creation (PostGIS spatial query), new merchant post scanning, outbox relay |
| &nbsp;&nbsp;`/Catalogue` | Category and tag taxonomy — create, rename, soft-disable; read access for post creation and merchant subscriptions |
| &nbsp;&nbsp;`/Admin` | User ban enforcement (delegates to `Auth`), taxonomy management (internal to `Catalogue` subfolder) |
| `Gdzie.Kupic.Chat` | Message persistence, thread management, image attachment references; defines `IChatChannel` |
| `Gdzie.Kupic.Notifications` | Push subscription management, deduplication, Web Push and email dispatch; defines `INotificationChannel` |
| `Gdzie.Kupic.Location` | Google Maps Geocoding API client — address → coordinates + display name conversion; called only during location save; never called at post creation or matching time |
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
- `Marketplace` → `Location`, `Notifications`, `Chat` (thread events only), `Hangfire`, `Auth` (Admin subfolder only)
- `Notifications` → `Hangfire`; defines `INotificationChannel`
- `Chat` → `Storage`, `Notifications` (in-app `newMessage` notification), `Hangfire`; defines `IChatChannel` and `IChatThreadEvents` (called by other modules after commit)
- `Location` → external HTTP client only (no module dependencies)
- `Storage` → no module dependencies
- `Hangfire` → no module dependencies
- `Auth` → no module dependencies
- No module may depend on `API` or `Realtime` (dependency flows inward only). Exception: `Realtime` references `API.Contract` for event names and payload types

---

## 3. Inter-Service Communication

- `gdzie-kupic-ui` → `gdzie-kupic-service`: REST over HTTPS for all data operations; SignalR WebSocket for real-time events (status panel, feed updates, chat)
- `gdzie-kupic-service` → Google Maps Geocoding API: called directly from `Gdzie.Kupic.Location` during location save only; never called during post creation or matching
- No message broker or event bus for MVP — all inter-module communication within `gdzie-kupic-service` is in-process

**Post-MVP consideration:** `Gdzie.Kupic.Matching` is the primary candidate for extraction into an independent service if subscription scoring, relevance ranking, or multiple merchant locations are introduced. The module boundary already isolates it for this purpose.

---

## 4. Data Architecture

- **Primary database**: PostgreSQL with PostGIS extension — used by `gdzie-kupic-service` for all persistent data
- **Spatial data**: `MerchantBranches.Coordinates` stored as `GEOGRAPHY(Point, 4326)`; spatial index required for radius match queries on post creation
- **Buyer saved locations**: stored in PostgreSQL as a collection of named coordinate points per buyer account; geocoded once on save, reused on post creation
- **Background jobs**: Hangfire job store persisted to the same PostgreSQL instance
- **Outbox table**: `Outbox` records are written in the same transaction as the triggering domain write; processed by the outbox relay and never deleted — retained for audit
- **File storage**: chat image attachments stored in an S3-compatible object store; application code uses the S3 API exclusively — storage backend is swappable via configuration

---

## 5. Authentication & Authorization

- JWT-based: access token (default 7 days, configurable) + refresh token stored in the database
- Refresh tokens are rotated on every use; previous token is invalidated immediately
- Account status (active / banned) is checked on every authenticated request; result is short-lived cached (~1 minute) to limit database load
- Banned accounts are rejected immediately regardless of token validity
- Three roles: `Buyer`, `Merchant`, `Admin` — enforced at the API layer; no anonymous access

---

## 6. Real-Time Communication

- Transport: SignalR - a single persistent connection per client to one hub (`AppHub`, `/hubs/app`), shared for all real-time events
- **Authentication**: any authenticated role. The JWT is sent in the `access_token` query string (browsers cannot set headers on WebSockets) and validated exactly like REST, including the ban check; account status is checked only on connect. Only paths under `/hubs` accept the query-string token. CORS is configured without credentials (the token is not a cookie)
- **Groups**: on connect the connection joins the group `user:{userId}`; there are no per-post or per-thread groups. The hub has no client-callable methods - the server only pushes
- **Events are thin** (camelCase, identifiers only); the client refetches the data over REST, so authorization stays in one place:

| Event | Payload |
|-------|---------|
| `postAdded` | `{ postId }` |
| `postRemoved` | `{ postId }` |
| `postStatusChanged` | `{ postId }` |
| `messageReceived` | `{ threadId, messageId }` |
| `threadUpdated` | `{ threadId }` |
| `notificationRaised` | `{ kind: merchantResponded \| newMessage, postId \| null, threadId \| null }` |

- `resync` is a client-local concept (refetch after reconnect); the server never sends it
- Events are sent after the database transaction commits. A failed push is caught and logged and never fails the request or job; there is no outbox. All updates are derived from persisted state - the database is the source of truth, SignalR is delivery only
- MVP targets a single application instance; no distributed SignalR backplane (e.g. Redis) required

**Per-module channel interfaces:**
- Each module that needs to push real-time events defines its own channel interface as part of its public contract - no SignalR dependency. Channel methods take plain parameters and recipient **user** ids (merchant ids are resolved to the user ids of all merchant accounts by the module)
- `Gdzie.Kupic.Marketplace` defines `IPostFeedChannel` - `postAdded`, `postRemoved`, `postStatusChanged`
- `Gdzie.Kupic.Chat` defines `IChatChannel` - `messageReceived`, `threadUpdated`
- `Gdzie.Kupic.Notifications` defines `INotificationChannel` - `notificationRaised`
- `Gdzie.Kupic.Realtime` implements all three interfaces on top of `IHubContext<AppHub>` and is the only module with a SignalR reference. Event names and payload types live in `Gdzie.Kupic.API.Contract/Realtime`

**Module registration pattern:**
- `Realtime` exposes a `RealtimeBuilder` with `AddChannel<TInterface, TImpl>()` (`where TImpl : class, TInterface`); each call produces exactly one singleton DI registration
- The full channel manifest lives in `Program.cs` - explicit, readable, no implicit wiring:

```csharp
builder.Services.AddRealtimeModule(realtime => realtime
    .AddChannel<IPostFeedChannel, PostFeedChannel>()
    .AddChannel<IChatChannel, ChatChannel>()
    .AddChannel<INotificationChannel, NotificationChannel>());

app.MapRealtimeHubs();
```

(The manifest grows as the channels are implemented; `MapRealtimeHubs()` maps `AppHub` at `/hubs/app`.)

### Out-of-app delivery (Web Push)

Every notification goes through `INotificationDispatcher.DispatchAsync(Notification { Kind, RecipientUserId, PostId?, ThreadId? })`, called right after the in-app push (so after `SaveChangesAsync`) for `NewPost` (per user account of a newly notified merchant), `MerchantResponded` (buyer, positive states) and `NewMessage` (other participant). The dispatcher never throws. It does nothing when VAPID is not configured or when the recipient has an open `AppHub` connection (`IPresenceTracker`, an in-memory per-user connection set maintained by `AppHub`; single instance, like the hub itself). Otherwise it enqueues one `SendWebPushJob` per registered device. The job builds the Polish payload `{ kind, postId, threadId, title, body }` on the server (never message content), sends it with Lib.Net.Http.WebPush and, for `NewPost`, sets `PostNotifications.Channel/SentAt` only after a real delivery. A 404/410 from the push service enqueues `CleanPushSubscriptionsJob(endpoint)`; 5xx/429/network errors throw so Hangfire retries that one device.

---

## 7. Background Jobs

- **Runtime**: Hangfire, hosted in-process within `gdzie-kupic-service`
- **Job store**: PostgreSQL (same instance as application data, dedicated `hangfire` schema)
- **Abstraction**: the `Hangfire` module exposes a job scheduling interface (enqueue / schedule / recurring); other modules never reference Hangfire directly
- **Dashboard**: enabled in the Development environment only; job failures are logged (Seq) with `jobId`, `postId`, `correlationId`
- **Tests**: the Hangfire server is disabled in the `Testing` environment; job classes are invoked directly

**Outbox pattern for reliable job dispatch:**
- Post creation writes a `Post` record and an `Outbox` entry (`type: NotifyMerchants`, `payload: postId`) in a single database transaction
- The outbox relay is a hosted background service (`PeriodicTimer`, ~5 seconds, configurable) — not a Hangfire recurring job, since recurring jobs are cron-based with a 1-minute minimum resolution. It reads unprocessed entries in batches (`SELECT … FOR UPDATE SKIP LOCKED` inside a transaction), enqueues the corresponding fan-out job in Hangfire, and sets `ProcessedAt`
- Enqueue and `ProcessedAt` are not one atomic operation, so delivery is at-least-once; fan-out jobs are idempotent
- This guarantees that a persisted post always results in merchant notification dispatch, even if the application crashes between post creation and job enqueue
- Fan-out jobs are idempotent — safe to retry on failure

**Job inventory:**

| Job | Trigger | Description |
|---|---|---|
| Outbox relay (hosted service, not a Hangfire job) | Polling (~5s interval) | Picks up unprocessed outbox entries and enqueues downstream jobs |
| `NotifyMerchantsJob` | Enqueued by outbox relay on post creation | Queries matched merchants via PostGIS (shared matching query), enqueues batch jobs (50 merchants each) that write `PostNotification` records and hand them to the notification dispatcher, then updates post `NotificationDispatchStatus` to `Dispatched` |
| `NotifyNewMerchantJob` | Enqueued via outbox on merchant onboarding completion and on subscription added | Scans `Active`, non-expired posts matching the merchant's location and subscriptions (regardless of dispatch status); writes notifications for matches not already in `PostNotification` |
| `ExpirePostsJob` | Scheduled (periodic, ~1 min, configurable) | Transitions posts past their expiry deadline to `Expired` state |
| `CleanPushSubscriptionsJob` | Triggered on delivery failure | Removes invalid or expired push subscription endpoints |

**Deduplication:**
- `PostNotification` has a unique constraint on `(PostId, MerchantId)`
- All notification dispatch jobs use `INSERT ... ON CONFLICT DO NOTHING` — retries and the new-merchant job are both naturally idempotent
- A merchant can never receive two push notifications for the same post

**Post notification dispatch status:**
- `Post` carries a `NotificationDispatchStatus` field: `Pending → Dispatched`
- The buyer status panel shows "Looking for merchants in your area..." while status is `Pending`
- Once `NotifyMerchantsJob` has enqueued all batches, status transitions to `Dispatched` and the panel switches to "Notified X merchants" (or zero-match popup if count is 0); the count is derived live and may still grow for a few seconds
- Until real-time delivery (Phase 6) the UI polls the status endpoint while status is `Pending`

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
- Maximum attachment size is configurable (`Chat:MaxAttachmentBytes`, default 5 MB)
- **Upload path**: the client sends `multipart/form-data` (`body`, `image`) to `POST /api/chat/threads/{id}/messages`. The API checks the size and the type (JPEG, PNG or WebP by magic bytes, one image per message), stores the object under `chat/{threadId}/{messageId}.{ext}` and only then creates the message, so a message never points to a missing object
- **Private bucket**: messages expose `attachmentUrl`, a presigned GET (about 15 minutes, `Chat:AttachmentUrlLifetimeMinutes`) generated on every read after the thread participation check
- **Abstraction**: `IObjectStorage` (put, presigned GET, delete) in `Gdzie.Kupic.Chat`, implemented on AWSSDK.S3; endpoint, bucket and credentials come from the `Storage` configuration section. `Storage:PublicEndpoint` is the host browsers use (inside Docker the API talks to `http://minio:9000`, browsers to `http://localhost:9000`)
- Attachments live as long as the thread; clean-up is out of scope for now

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
- Nginx acts as a reverse proxy: routes `/api` and SignalR (`/hubs`, with `Upgrade`/`Connection` headers for WebSockets and a long read timeout) traffic to `gdzie-kupic-service`, serves `gdzie-kupic-ui` static assets
- TLS termination at Nginx

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
