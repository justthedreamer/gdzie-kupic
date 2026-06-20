# Project Planning

Issues are organised into **phases** (GitHub Epics). Each phase delivers a demonstrable working slice across all relevant repos.
Tickets are minimum **M** size — S-size work is merged into the nearest logical ticket.

| Size | Rough duration |
|---|---|
| S | Half a day or less |
| M | ~1 day |
| L | 2–3 days |
| XL | 4–5 days |
| XXL | 1 week+ |

---

## Phases (Epics)

| Phase | Name | Deliverable |
|---|---|---|
| P1 | Infrastructure & Scaffolding | All repos compile; full Docker Compose stack starts; health endpoints respond |
| P2 | Authentication | Register and log in as Buyer, MerchantAccount, or Admin; JWT + refresh token flow working end-to-end |
| P3 | Catalogue, Locations & Merchant Onboarding | Admin can manage taxonomy; buyer has saved locations; merchant is fully onboarded with location + subscriptions |
| P4 | Post Lifecycle & Matching | Buyer can create a post; matched merchants are queued for notification; status panel shows dispatch state |
| P5 | Merchant Response & Chat | Merchant can respond to a post; chat thread opens; full core loop works without real-time |
| P6 | Real-Time | Status panel, merchant feed, and chat are all live via SignalR |
| P7 | Push Notifications & Email | Merchants and buyers receive notifications when the app is not open |
| P8 | Admin & Moderation | Admin can ban accounts with full cascading effects |
| P9 | Testing & Release | Test suites in place; production deployment config ready; MVP shipped |

---

## `gdzie-kupic-service`

### Phase 1.1 — Service Scaffold & Infrastructure

- Solution scaffold: all `Gdzie.Kupic.*` projects, project references, NuGet baseline — **M**
- Docker Compose stack: PostgreSQL + PostGIS, MinIO, Seq, all application services with health checks — **M**
- EF Core baseline: DbContext, PostGIS extension, Serilog + Seq structured logging pipeline, initial schema migration — **M**
- Location module: reverse geocoding endpoint via Google Geocoding API (`GET api/location`) — **M**

---

### Phase 2.1 — Authentication

- User model + registration (Buyer + MerchantAccount) + login + JWT issuance + refresh token rotation — **L**
- User mock for testing: hardcoded long-lived access token; credentials committed to docs — **M**
- Account status enforcement per-request (~1 min cache); banned account rejection; concurrent refresh token theft detection (revoke all tokens on collision) — **M**
- Administrator mock: hardcoded long-lived access token; credentials committed to docs — **M**
- CORS: service accepts requests only from the frontend domain — **M**
- Location controller requires authentication — **M**

---

### Phase 3.1 — Catalogue, Locations & Merchant Onboarding

- Define and provision catalogue: predefined categories and tags seeded in code with stable IDs, always present in the database — **M**
- Category and tag management: admin CRUD, soft-disable, read endpoints; `IMemoryCache` with invalidation on admin write — **M**
- Buyer saved locations: add (browser geolocation + manual address via Google Maps API), list, delete — **M**
- Merchant onboarding: create `Merchant` + `MerchantBranch` + `MerchantAccount` in one transaction + branch location setup — **L**
- Category/tag subscription management: add, remove, list — **M**

---

### Phase 4 — Post Lifecycle & Matching

- Post creation endpoint: write `Post` + `Outbox` entry in single transaction; buyer close + fulfil transitions; `ExpirePostsJob` — **L**
- `OutboxRelayJob` + `NotifyMerchantsJob`: PostGIS radius + subscription query, `PostNotification` dedup, fan-out batching (50/batch), `NotificationDispatchStatus` → `Dispatched` — **XL**
- `NotifyNewMerchantJob`: on merchant registration, scan active posts, dispatch for unnotified matches — **M**
- Zero-match handling: long-lived post opt-in flag; status panel data endpoint (notified / checking / response state counts) — **M**

---

### Phase 5 — Merchant Response & Chat

- Response state machine: submit + state transitions, response lock on post closure, race condition rejection — **M**
- `ChatThread` creation on first positive response; `ChatMessage` persistence; message list endpoint; unread count tracking — **L**
- Image attachment upload via `Gdzie.Kupic.Storage` (MinIO/S3); thread lock on participant ban — **M**

---

### Phase 6 — Real-Time (SignalR)

- `RealtimeBuilder` infrastructure: `AddHub<TInterface, THub>()` with compile-time constraint; `Program.cs` manifest — **M**
- `IPostFeedChannel` hub: buyer status panel live count push (per-post channel) + merchant feed new post / post removal push — **L**
- `IChatChannel` hub: per-thread new message push; `INotificationChannel` hub: in-app event push to buyers and merchants — **M**

---

### Phase 7 — Push Notifications & Email

- Push subscription registration + storage; Web Push (VAPID) dispatch in notification flow; `CleanPushSubscriptionsJob` — **L**
- SendGrid email fallback; per-user email opt-in setting — **M**

---

### Phase 8 — Admin & Moderation

- Ban buyer: expire all active posts, lock all chat threads — **M**
- Ban merchant: lock responses + chat threads, remove push subscriptions — **M**

---

### Phase 9 — Testing & Release

- Unit test suite: core domain logic (state machines, matching rules) — **M**
- Functional test suite: Testcontainers (PostgreSQL + PostGIS); auth + post lifecycle + matching test coverage — **L**
- E2E smoke tests: critical path (register → post → match → respond → chat) — **M**
- Production deployment: `docker-compose` production variant, Nginx reverse proxy + TLS config — **M**

---

## `gdzie-kupic-ui`

### Phase 1.2 — Frontend Scaffold

- Frontend repository scaffold: Vue, Quasar, Vite, base folder structure; HTTP client for backend service — **M**
- Location component: read location via browser Geolocation API, call `GET api/location`, display result to the user — **M**
- Base styling: color palette, base utility components — **L**

---

### Phase 2.2 — Authentication

- Redirect to sign-in page when the user is not authenticated — **M**
- Display buttons to log in as Admin or User on the test instance — **M**
- Display buttons to switch account or log out — **M**
- Access token stored and attached to every outgoing request — **M**

---

### Phase 3.2 — Catalogue, Locations & Merchant Onboarding

- CRUD component(s) for category management (admin) — **M**
- Buyer saved location management: add via browser geolocation + manual address input, list, delete — **M**
- Merchant onboarding flow: branch location setup + category/tag subscription setup — **L**

---

### Phase 4 — Post Lifecycle

- Post creation form: location picker, category/tag selector, urgency flag + deadline, description — **M**
- Post list + post detail + status panel (REST-based; shows dispatch state + response counts) + zero-match popup + long-lived opt-in — **L**

---

### Phase 5 — Merchant Response & Chat

- Merchant feed: infinite scroll, urgent-first ordering, unanswered vs. responded split — **L**
- Post detail + response state machine UI: 4 response states, state change, lock on closed post, race condition error — **M**
- Chat views: buyer and merchant perspectives; thread list, message thread, image upload — **L**

---

### Phase 6 — Real-Time

- SignalR client: shared persistent connection, channel subscription helpers — **M**
- Live status panel + real-time merchant feed (post added / removed) + real-time chat delivery — **L**
- Non-dismissible banner + disabled actions when post closes while merchant has detail open — **S** *(merge into feed ticket)*

---

### Phase 7 — Push Notifications & PWA

- PWA manifest + service worker; push notification permission request + subscription registration — **M**
- In-app notification display (`INotificationChannel` events) — **M**

---

### Phase 8 — Admin UI

- Admin ban UI: ban buyer + ban merchant — **M**

---

## Critical Path

P1 → P2 → P3 → P4 → P5 → P6 → P7 → P8 → P9

Working demo (core loop without real-time) is reachable after **P5**.
Full MVP is complete after **P9**.

**Safe to defer post-submission**: functional + E2E tests beyond unit tests (P9), email fallback (P7), admin UI (P8).
