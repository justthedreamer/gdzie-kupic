# Functional & Non-Functional Requirements

Derived from `design-decisions.md`, `data-model.md`, and `architecture.md`.

---

## Functional Requirements

### FR-AUTH — Authentication & Authorization

- **FR-AUTH-1** — Buyers and MerchantAccounts self-register; Admin accounts are provisioned directly with no self-registration path
- **FR-AUTH-2** — Login returns a JWT access token (default lifetime: 7 days, configurable) and a refresh token stored in the database
- **FR-AUTH-3** — Refresh tokens are rotated on every use; the previous token is invalidated immediately
- **FR-AUTH-3a** — Concurrent rotation edge case: if two requests arrive simultaneously with the same refresh token, the second will see a revoked token — this is treated as a token theft signal; all refresh tokens for that user are immediately revoked and the user is forced to log in again
- **FR-AUTH-4** — Account status (`Active` / `Banned`) is checked on every authenticated request; a banned account's access token is rejected regardless of expiry
- **FR-AUTH-5** — Password reset is via a one-time email link; on use, the link is invalidated and the user sets a new password
- **FR-AUTH-6** — No anonymous or guest access; all endpoints require authentication
- **FR-AUTH-7** — Three roles: `Buyer`, `MerchantAccount`, `Admin`; permissions are enforced at the API layer

---

### FR-LOCATION — Location & Geocoding

- **FR-LOC-1** — A buyer must add at least one saved location before creating a post
- **FR-LOC-2** — A buyer can manage any number of named saved locations (e.g. "Home", "Office") at any time
- **FR-LOC-3** — Location input — two methods: (a) "Find me" uses the browser Geolocation API — coordinates arrive directly from the frontend, no geocoding required; (b) manual address entry — geocoded once via `gdzie-kupic-location-service`, result stored as coordinates + display name
- **FR-LOC-4** — Geocoding happens only when a location is saved; never at post creation time or during matching
- **FR-LOC-5** — Coordinates are stored as `GEOGRAPHY(Point, 4326)`
- **FR-LOC-6** — Merchant sets their branch location during onboarding using the same two input methods as buyers

---

### FR-POST — Post Lifecycle

- **FR-POST-1** — A buyer creates a post by selecting a saved location, setting a search radius, choosing a category and tag (both required), and providing a title, optional description, and urgency flag
- **FR-POST-2** — Post creation is a pure database write; no geocoding, no synchronous external calls
- **FR-POST-3** — Post creation writes the `Post` record and an `Outbox` entry in a single database transaction
- **FR-POST-4** — Post states: `Active → Fulfilled / Closed / Expired`
  - `Fulfilled` — buyer found what they needed through the platform
  - `Closed` — buyer cancelled or found it elsewhere
  - `Expired` — post reached its expiry deadline without manual closure
- **FR-POST-5** — Posts auto-expire after 72 hours (default, configurable)
- **FR-POST-6** — Urgent posts: buyer sets an urgency flag and specifies a deadline date; the post expires at that deadline instead of the default duration; urgency is displayed to merchants on the post card
- **FR-POST-7** — Long-lived posts: buyer can opt in via the zero-match popup to extend expiry to 14 days (configurable)
- **FR-POST-8** — Buyer can transition their post to `Fulfilled` or `Closed` at any time while it is `Active`
- **FR-POST-9** — When a post transitions out of `Active`, all merchant response state changes are locked
- **FR-POST-10** — A `Post` carries `NotificationDispatchStatus`: `Pending` (default) → `Dispatched` (set by `NotifyMerchantsJob` on completion)

---

### FR-MATCH — Merchant Matching & Notification Dispatch

- **FR-MATCH-1** — On post creation, an `OutboxRelayJob` (scheduled every ~5 seconds) picks up the `Outbox` entry and enqueues `NotifyMerchantsJob`
- **FR-MATCH-2** — `NotifyMerchantsJob` queries `MerchantBranches` via PostGIS: matches branches whose `Coordinates` fall within the post's radius + 3 km tolerance buffer AND whose `Merchant` is subscribed to the post's category or tag
- **FR-MATCH-3** — A category-level `MerchantSubscription` (null `TagId`) matches all posts in that category regardless of tag; a tag-level subscription matches only the exact tag
- **FR-MATCH-4** — For each matched merchant: write a `PostNotification` record, then dispatch Web Push or email fallback
- **FR-MATCH-5** — `PostNotification` has a unique constraint on `(PostId, MerchantId)`; all dispatch jobs use `INSERT … ON CONFLICT DO NOTHING` — a merchant can never receive two notifications for the same post
- **FR-MATCH-6** — Fan-out is batched (50 merchants per batch); each batch is an independently retried child job
- **FR-MATCH-7** — On completion, `NotifyMerchantsJob` sets `Post.NotificationDispatchStatus = Dispatched`
- **FR-MATCH-8** — Zero-match result: `NotificationDispatchStatus` transitions to `Dispatched` with a count of 0; buyer is shown the zero-match popup and can opt in to a long-lived post
- **FR-MATCH-9** — `NotifyNewMerchantJob` is enqueued on merchant registration completion; it scans active posts matching the new merchant's branch location and subscriptions and dispatches notifications for any matches not already recorded in `PostNotification`

---

### FR-RESPONSE — Merchant Response State Machine

- **FR-RESP-1** — A merchant can respond to any post in their feed with one of four states:
  - `CantHelp` — archived for the merchant (shown blurred); reversible while the post is active
  - `MayHaveIt` — opens a chat thread with the buyer
  - `HaveIt` — opens a chat thread with the buyer
  - `CanOrderIt` — opens a chat thread with the buyer (item not in stock but can be sourced)
- **FR-RESP-2** — Any transition to `MayHaveIt`, `HaveIt`, or `CanOrderIt` creates a `ChatThread` if one does not already exist for that `(PostId, MerchantId)` pair
- **FR-RESP-3** — A merchant can change their response state at any time while the post is `Active`
- **FR-RESP-4** — Response state changes are rejected by the server once the post is `Fulfilled`, `Closed`, or `Expired`
- **FR-RESP-5** — If a merchant submits a response in the race window after a post closes, the server rejects it and the client shows an error notification

---

### FR-CHAT — Chat

- **FR-CHAT-1** — One chat thread per merchant per post (`ChatThread` unique on `(PostId, MerchantId)`)
- **FR-CHAT-2** — A thread is created on the first positive merchant response (`MayHaveIt`, `HaveIt`, `CanOrderIt`)
- **FR-CHAT-3** — Reopening a post detail after a thread exists shows the existing thread — no duplicate threads
- **FR-CHAT-4** — Messages support text and image attachments; images are stored in S3-compatible storage by key; maximum attachment size is configurable (default 5 MB)
- **FR-CHAT-5** — Message history is persisted and available on reconnect or page reload
- **FR-CHAT-6** — Chat threads remain open after the post is `Closed`, `Fulfilled`, or `Expired`; buyer and merchant can still coordinate
- **FR-CHAT-7** — A thread is locked (no new messages from either side) when either participant is banned
- **FR-CHAT-8** — Unread message counts are shown in the buyer and merchant inbox views

---

### FR-FEED — Merchant Feed

- **FR-FEED-1** — The merchant feed shows active posts matching the merchant's branch location and subscriptions
- **FR-FEED-2** — Posts the merchant has not yet responded to appear in the main feed; responded posts appear in a separate flat list with the merchant's response state visible
- **FR-FEED-3** — Default ordering: urgent posts first, then newest first within each group
- **FR-FEED-4** — Feed loads via infinite scroll; no pagination UI
- **FR-FEED-5** — When a post transitions to `Closed`, `Fulfilled`, or `Expired`, it is removed from the merchant feed in real-time via SignalR
- **FR-FEED-6** — If the merchant has a post detail open when it closes, a non-dismissible banner is shown and all action buttons are disabled

---

### FR-CATALOGUE — Category & Tag Taxonomy

- **FR-CAT-1** — Admin can create, rename, and soft-disable categories and tags
- **FR-CAT-2** — Tags belong to exactly one category; tag names are unique within a category
- **FR-CAT-3** — Soft-disabling a tag: existing merchant subscriptions remain active; the tag cannot be selected on new posts; no data is deleted; reversible
- **FR-CAT-4** — Post creation requires exactly one category and one tag
- **FR-CAT-5** — Merchant subscription: subscribe to a whole category (catch-all) or to specific tags within a category, or both

---

### FR-ADMIN — Admin

- **FR-ADMIN-1** — Admin manages the category and tag taxonomy (create, rename, soft-disable)
- **FR-ADMIN-2** — Admin can ban any user account (buyer or MerchantAccount); ban takes effect immediately
- **FR-ADMIN-3** — **Buyer ban effects**: all active posts are immediately expired; all chat threads involving the buyer are locked; merchants see a notice in each affected thread
- **FR-ADMIN-4** — **Merchant ban effects**: merchant cannot submit new responses; existing responses remain visible to buyers; all chat threads with that merchant are locked; buyers see a notice in each affected thread; merchant's push subscriptions are removed
- **FR-ADMIN-5** — Admin account is created by a startup seeder; credentials (`AdminSeed:Email`, `AdminSeed:Password`) are read from configuration; seeder runs only if no admin account exists yet; idempotent — safe to run on every startup
- **FR-ADMIN-6** — For local development, admin credentials are defined in `appsettings.Local.json` (gitignored); no self-registration path exists for admin accounts

---

### FR-NOTIF — Push Notifications & Fallback

- **FR-NOTIF-1** — Each device registers a Web Push subscription endpoint stored in `PushSubscriptions`
- **FR-NOTIF-2** — Notification delivery hierarchy per event:
  1. SignalR — delivered in-app if the client has an active connection
  2. Web Push — sent to the device if SignalR is not connected
  3. Email — additional fallback; opt-in per user in account settings (disabled by default)
- **FR-NOTIF-3** — Push notifications are sent for: new post matching merchant subscriptions, new chat message, merchant response received by buyer
- **FR-NOTIF-4** — Invalid or expired push subscription endpoints are removed when delivery fails (`CleanPushSubscriptionsJob`)
- **FR-NOTIF-5** — Email fallback uses SendGrid; opt-in is per user and stored in account settings

---

## Non-Functional Requirements

### NFR-PERF — Performance

- **NFR-PERF-1** — PostGIS GIST spatial index on `MerchantBranches.Coordinates` is required; the radius match query must be index-backed at all times
- **NFR-PERF-2** — PostGIS GIST spatial index on `SavedLocations.Coordinates` for future buyer proximity features
- **NFR-PERF-3** — Account status (`Active` / `Banned`) lookup is cached per-request with a ~1 minute TTL to limit database load under concurrent traffic
- **NFR-PERF-3a** — Category and tag taxonomy is cached in-memory (`IMemoryCache`); cache is invalidated on any admin write (create, rename, soft-disable); TTL fallback of 5 minutes; taxonomy is read on almost every request (post creation, feed load, subscriptions) and written only by admin
- **NFR-PERF-4** — Outbox relay polling interval: ~5 seconds (configurable); Hangfire polling interval: 1–2 seconds for near-real-time notification dispatch
- **NFR-PERF-5** — Hangfire worker count: `ProcessorCount × 5` (configurable)

---

### NFR-SCALE — Scalability

- **NFR-SCALE-1** — MVP targets a single application instance; no distributed SignalR backplane (e.g. Redis) required
- **NFR-SCALE-2** — Fan-out batching (50 merchants per batch) prevents a single large post from blocking a worker thread indefinitely
- **NFR-SCALE-3** — Hangfire job queue is persisted in PostgreSQL; job depth is unlimited and survives application restarts
- **NFR-SCALE-4** — Module boundaries in `gdzie-kupic-service` are designed to allow future extraction of `Gdzie.Kupic.Matching` into an independent service without a rewrite

---

### NFR-SEC — Security

- **NFR-SEC-1** — Passwords stored as bcrypt hashes; plaintext passwords never persisted or logged
- **NFR-SEC-2** — Refresh token values are stored as hashes; raw token values are never persisted
- **NFR-SEC-3** — Banned accounts are rejected on every request regardless of token validity or expiry
- **NFR-SEC-4** — `gdzie-kupic-location-service` is internal — not exposed to the public internet; no auth required between services for MVP
- **NFR-SEC-5** — All public endpoints require a valid JWT; no anonymous access
- **NFR-SEC-6** — Post content is private — posts are visible only to their owner and merchants matched to that post; no public browsing

---

### NFR-REL — Reliability

- **NFR-REL-1** — Post creation and outbox entry are written in a single database transaction; a persisted post always results in merchant notification dispatch, even if the application crashes between post creation and job enqueue
- **NFR-REL-2** — All notification dispatch jobs are idempotent (`INSERT … ON CONFLICT DO NOTHING`); safe to retry on failure
- **NFR-REL-3** — `Outbox` records are never deleted after processing — retained for audit
- **NFR-REL-4** — Hangfire's built-in retry and job history serves as the primary audit trail for dispatch failures

---

### NFR-OBS — Observability

- **NFR-OBS-1** — Serilog in all .NET services writes structured JSON logs to stdout; Seq collects and indexes them
- **NFR-OBS-2** — Every log entry is enriched with `serviceName`, `correlationId`, and `traceId` at minimum
- **NFR-OBS-3** — Every Hangfire job logs a structured entry with `jobId`, `postId`, and `correlationId` at start, completion, and each retry
- **NFR-OBS-4** — Each service exposes a `/health` endpoint
- **NFR-OBS-5** — Unhandled exceptions are logged to Seq with full stack traces; Sentry integration deferred post-MVP

---

### NFR-DEV — Developer Experience

- **NFR-DEV-1** — The full development environment runs entirely in Docker; a single `docker-compose.yml` brings up all infrastructure and application services
- **NFR-DEV-2** — MinIO runs as a Docker container in local dev, accessed via the same S3 API as production — no code changes needed to switch storage backends
- **NFR-DEV-3** — Functional tests use Testcontainers to spin up a real PostgreSQL + PostGIS instance; no mocked database in functional test scope
- **NFR-DEV-4** — Environment-specific configuration via `.env` files; secrets are never committed to source control
