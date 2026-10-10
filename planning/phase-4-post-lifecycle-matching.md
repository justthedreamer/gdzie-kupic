# Phase 4 — Post Lifecycle & Matching — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review) · `Ready` (approved) · `#<n>` (GitHub issue created)

**Deliverable:** A buyer creates a post, matching merchants are found and recorded, and the buyer sees live status (per [planning.md](../docs/planning.md)).

**Goal of this split:** Service and UI tickets can be implemented in parallel by two agents. The UI agent works against the **API contract** below (mocking responses until the Service tickets land); the Service agent implements exactly that contract.

---

## Epic - [Epic]: Phase 4: Post Lifecycle & Matching - [#75](https://github.com/justthedreamer/gdzie-kupic/issues/75)

**Sub-issues:**

| # | Ticket | Size | Depends on | Status |
|---|---|---|---|---|
| S1 | [Service]: Hangfire Infrastructure | M | — | [#76](https://github.com/justthedreamer/gdzie-kupic/issues/76) |
| S2 | [Service]: Post Creation & Lifecycle | L | S1 (for `ExpirePostsJob`) | [#77](https://github.com/justthedreamer/gdzie-kupic/issues/77) |
| S3 | [Service]: Outbox Relay & Merchant Matching (`NotifyMerchantsJob`) | XL | S1, S2 | [#78](https://github.com/justthedreamer/gdzie-kupic/issues/78) |
| S4 | [Service]: New Merchant Post Scan (`NotifyNewMerchantJob`) | M | S3 | [#79](https://github.com/justthedreamer/gdzie-kupic/issues/79) |
| S5 | [Service]: Zero-Match Handling & Post Status Endpoint | M | S2, S3 | [#80](https://github.com/justthedreamer/gdzie-kupic/issues/80) |
| U1 | [UI]: Post Creation Form | M | contract S2 | [#81](https://github.com/justthedreamer/gdzie-kupic/issues/81) |
| U2 | [UI]: Request List, Detail, Status Panel & Zero-Match | L | contract S2, S5 | [#82](https://github.com/justthedreamer/gdzie-kupic/issues/82) |

S1–S5 and U1–U2 map to the Phase 4 tasks in [planning.md](../docs/planning.md); S1 (Hangfire) was split out because the `Gdzie.Kupic.Hangfire` module is still empty. In the UI, a post is called a "request" (`/requests`, "zapytanie"); the API uses `/api/posts`.

---

## Decisions (agreed with the architect)

| # | Decision |
|---|---|
| 1 | Post creation takes coordinates directly (no geocoding, [FR-POST-2](../docs/requirements.md)). The UI offers the location form plus the buyer's saved locations; after a successful creation with a form-entered location it offers to save it under a name (existing saved-locations endpoint). |
| 2 | Radius: presets 5 / 10 / 25 / 50 km, any custom value > 0 (no upper bound), or **unlimited** (`radiusKm = null` → no spatial predicate in matching). |
| 3 | Exactly one category and one tag per post, both required and enabled. |
| 4 | Urgent post: deadline date + time, at least 1 h and at most 72 h ahead; the post expires at the deadline. |
| 5 | Long-lived opt-in (14 days, configurable): only for `Active`, `Dispatched`, non-urgent posts with zero matched merchants; extends `ExpiresAt` to now + 14 d. |
| 6 | Post state transitions only from `Active` (`Fulfilled`, `Closed`, `Expired`). `ExpirePostsJob` runs about every minute (configurable); operations that depend on an active post also check `ExpiresAt`. |
| 7 | Hangfire: PostgreSQL job store in the same DB (`hangfire` schema), scheduling abstraction so other modules never reference Hangfire, dashboard in Development only, server disabled in `Testing`. |
| 8 | Outbox relay is a hosted background service (~5 s, configurable), not a Hangfire recurring job (cron minimum is 1 minute). Fan-out jobs are Hangfire jobs. Outbox rows are never deleted; `ProcessedAt` only (no `Status`). Delivery is at-least-once; jobs are idempotent. |
| 9 | `PostNotification` gets `CreatedAt`; `Channel` / `SentAt` become nullable and are set by real dispatch in Phase 7. Phase 4 hands notifications to a no-op dispatcher seam. |
| 10 | Matching: branch within radius + 3 km, merchant subscribed to the category (category-level) or the exact tag; banned merchants and the post's author are excluded; one shared matching rule for `NotifyMerchantsJob` and `NotifyNewMerchantJob`. |
| 11 | Fan-out in batches of 50; `Dispatched` is set after all batches are enqueued (zero matches → `Dispatched` immediately); notified count is derived live. |
| 12 | `NotifyNewMerchantJob` runs on merchant onboarding completion **and** whenever a subscription is added (onboarding creates no subscriptions); scans `Active`, non-expired posts regardless of dispatch status. |
| 13 | Status panel: REST polling (5 s while `Pending`, then 30 s) until real-time arrives in Phase 6; response-state counts are 0 until Phase 5. |
| 14 | Buyer home: real requests + `LiveStatus` in Phase 4; activity and chats stay mocked/empty until Phase 5; the unused `budget` field is dropped. |

---

## Assumptions & open questions (resolved — approved by the architect)

1. **FR-LOC-1 vs. the creation form:** [FR-LOC-1](../docs/requirements.md) and [design-decisions.md §1](../docs/design-decisions.md) say a saved location is required before posting, while decision 1 lets a buyer create a post with a form-entered location without saving it. Assumed: relax FR-LOC-1 (saved location no longer required) and update both documents when approved. _Done: both documents updated._
2. **Field limits:** title required, max 120 characters; description optional, max 2000 characters. _Approved; now in FR-POST-1._
3. **Status endpoint shape:** a separate lightweight status endpoint for polling (not folded into the post detail).
4. **Buyer post list:** newest first, limit 50, no paging in MVP.
5. **Out of scope:** real Web Push / email dispatch (Phase 7), merchant responses and chat (Phase 5), real-time (Phase 6), moderation/ban of posts (later phases).

---

## API contract (shared between Service and UI tickets)

All endpoints require a valid access token and the **Buyer** role (`403` otherwise, `401` without a valid token). A buyer only ever sees their own posts (`404` for others' posts). Errors use the existing `ProblemDetails` format.

**Post shape** (`PostDto`): `{ id, title, description | null, latitude, longitude, radiusKm | null, category: { id, name }, tag: { id, name }, status, notificationDispatchStatus, isUrgent, urgentDeadline | null, expiresAt, isLongLived, createdAt }`

| Method + route | Request | Response |
|---|---|---|
| `POST /api/posts` | `{ latitude, longitude, radiusKm \| null, categoryId, tagId, title, description?, urgentDeadline? }` (`urgentDeadline` present = urgent post) | `201` `PostDto` · `400` validation (radius ≤ 0, deadline outside 1–72 h, tag not in category, disabled category/tag, field limits) |
| `GET /api/posts?scope=active\|ended` | — | `200` list of `PostDto & { notifiedCount }`, newest first, max 50; `active` = status `Active`, `ended` = `Fulfilled` / `Closed` / `Expired` |
| `GET /api/posts/{id}` | — | `200` `PostDto & { notifiedCount }` · `404` |
| `POST /api/posts/{id}/fulfil` | — | `204` · `404` · `409` post is not `Active` (or already past `ExpiresAt`) |
| `POST /api/posts/{id}/close` | — | `204` · `404` · `409` post is not `Active` (or already past `ExpiresAt`) |
| `GET /api/posts/{id}/status` | — | `200` `{ notificationDispatchStatus, notifiedCount, checkingCount, haveItCount, mayHaveItCount, canOrderItCount, cannotHelpCount, isZeroMatch }` · `404`; response-state counts are 0 until Phase 5; `isZeroMatch` = `Dispatched` and `notifiedCount = 0` |
| `POST /api/posts/{id}/long-lived` | — | `200` `PostDto` with extended `expiresAt` · `404` · `409` not eligible (not `Active`, not `Dispatched`, urgent, already long-lived, or has matches) |

---

## Drafted — `[Service]`: Hangfire Infrastructure

**Size:** M

**Brief Description**
Background job infrastructure is available to all modules: persistent job queue, workers, job logging and a scheduling facade.

**User Story**
As a developer, I want a reliable background job infrastructure so that notification and expiry work runs asynchronously and survives restarts.

**Description**
The `Gdzie.Kupic.Hangfire` module exists but is empty. This ticket delivers the Hangfire runtime hosted in-process, with the job queue persisted in the application's PostgreSQL database (own schema). Other modules schedule work only through an interface exposed by the module and never reference Hangfire directly. Worker count and polling interval follow the performance requirements and are configurable. Job executions are logged with the identifiers needed to trace a post. The dashboard is available only in the Development environment, and the job server is disabled when running tests so that job classes can be invoked directly.

**Documentation:** [architecture.md § Background Jobs](../docs/architecture.md) · [requirements.md](../docs/requirements.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** NFR-PERF-4, NFR-PERF-5, NFR-SCALE-3, NFR-REL-4, NFR-OBS-3

**Definition of Done**
- [ ] Jobs enqueued through the module's interface are persisted in PostgreSQL (dedicated schema) and run after an application restart
- [ ] The module exposes an interface for enqueuing, delayed scheduling and recurring jobs; no other module references Hangfire directly
- [ ] Worker count defaults to `ProcessorCount × 5` and polling to 1–2 s; both are configurable
- [ ] Job start, success and failure are logged with `jobId`, `postId` (when applicable) and `correlationId`
- [ ] The Hangfire dashboard is reachable in Development only and not exposed in other environments
- [ ] In the `Testing` environment no job server runs, and integration tests can execute job classes directly
- [ ] Documentation of the local stack mentions the new schema and dashboard
- [ ] Integration test proves an enqueued job is executed and a failing job is retried

---

## Drafted — `[Service]`: Post Creation & Lifecycle

**Size:** L

**Brief Description**
A buyer can create, list, view, fulfil and close posts; posts expire automatically.

**User Story**
As a buyer, I want to post what I am looking for so that nearby merchants can help me find it.

**Description**
Implements the post endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets): persistence for posts and the outbox, and creation that writes the post and its outbox entry in a single database transaction with no external calls. Radius may be a positive value or unlimited. Urgent posts expire at their deadline (1–72 h ahead); other posts expire after the configurable default of 72 h. The buyer can move an `Active` post to `Fulfilled` or `Closed`; all transitions start from `Active` only, and operations on a post past its `ExpiresAt` are rejected even before the periodic expiry job has run. A periodic `ExpirePostsJob` (about every minute, configurable) moves overdue posts to `Expired`. The outbox entry is consumed by the matching ticket (S3).

**Documentation:** [requirements.md § FR-POST](../docs/requirements.md) · [data-model.md § Posts, Outbox](../docs/data-model.md) · [architecture.md](../docs/architecture.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-POST-1, FR-POST-2, FR-POST-3, FR-POST-4, FR-POST-5, FR-POST-6, FR-POST-8, FR-POST-9, FR-POST-10, NFR-REL-1, NFR-REL-3

**Definition of Done**
- [ ] `Posts` and `Outbox` tables exist via a migration (nullable radius, outbox with `ProcessedAt` and a partial index on unprocessed entries)
- [ ] `POST /api/posts` creates a post with status `Active` and `NotificationDispatchStatus = Pending` plus an outbox entry of type `NotifyMerchants` in one transaction; a failure of either rolls back both
- [ ] Validation rejects radius ≤ 0, urgent deadlines earlier than 1 h or later than 72 h, a tag that does not belong to the category, disabled categories/tags, and over-long title/description
- [ ] Unlimited radius is stored as `null`
- [ ] Non-urgent posts expire after the configured default; urgent posts expire at their deadline
- [ ] `fulfil` and `close` work only on `Active`, non-expired posts of the calling buyer and return `409` otherwise; other buyers get `404`
- [ ] `GET /api/posts` returns only the caller's posts, filtered by `scope`, newest first, with `notifiedCount`
- [ ] `ExpirePostsJob` moves overdue `Active` posts to `Expired` and is idempotent
- [ ] Unit tests cover state transitions and expiry rules; integration tests cover the endpoints, ownership, the transactional outbox write and the expiry job

---

## Drafted — `[Service]`: Outbox Relay & Merchant Matching (`NotifyMerchantsJob`)

**Size:** XL

**Brief Description**
Every created post is matched to relevant merchants, each matched merchant gets exactly one notification record, and the post is marked as dispatched.

**User Story**
As a buyer, I want merchants who sell what I need near me to be notified automatically so that I get answers quickly.

**Description**
A background relay (about every 5 s, configurable) picks up unprocessed outbox entries and enqueues the corresponding job; unprocessed entries are never lost or processed twice concurrently, and a crash between enqueue and marking leads at worst to a harmless repeat. `NotifyMerchantsJob` finds merchants whose branch lies within the post's radius plus a 3 km tolerance (or any distance when the radius is unlimited) and who are subscribed to the post's category or exact tag; banned merchants and the post's author are excluded. Matched merchants are processed in batches of 50, each batch an independently retried job that records one `PostNotification` per merchant and post (duplicates are impossible) and hands it to a notification dispatcher. Real Web Push / email dispatch is Phase 7, so the dispatcher does nothing yet and `Channel` / `SentAt` stay empty. After all batches are enqueued the post becomes `Dispatched`; zero matches mark it `Dispatched` immediately. The matching rule is reusable by the new-merchant scan (S4).

**Documentation:** [requirements.md § FR-MATCH](../docs/requirements.md) · [architecture.md § Background Jobs](../docs/architecture.md) · [data-model.md § PostNotifications, Outbox](../docs/data-model.md) · [design-decisions.md §1–2](../docs/design-decisions.md)
**Requirements:** FR-MATCH-1, FR-MATCH-2, FR-MATCH-3, FR-MATCH-4, FR-MATCH-5, FR-MATCH-6, FR-MATCH-7, FR-MATCH-8, NFR-PERF-4, NFR-REL-1, NFR-REL-3

**Definition of Done**
- [ ] `PostNotifications` table exists via a migration with unique `(PostId, MerchantId)`, `CreatedAt`, and nullable `Channel` / `SentAt`; an index on `MerchantSubscriptions (CategoryId, TagId)` exists
- [ ] A post created through the API is picked up by the relay within about 5 s and marked as processed exactly once; the interval is configurable
- [ ] Two relay runs cannot process the same outbox entry concurrently; a repeated run for the same post creates no duplicate notifications
- [ ] A merchant is matched when a branch is within radius + 3 km and the merchant has a category-level subscription for the post's category or a subscription for the exact tag; a tag-level subscription for another tag does not match
- [ ] An unlimited-radius post matches every merchant with a matching subscription regardless of distance
- [ ] Banned merchants and the post's author are never matched
- [ ] Matched merchants are processed in batches of 50; a failing batch is retried without affecting other batches
- [ ] Each matched merchant gets exactly one `PostNotification`, and the notification dispatcher is invoked for it (no-op in this phase)
- [ ] `NotificationDispatchStatus` becomes `Dispatched` after all batches are enqueued, and immediately when there are no matches
- [ ] Job logs contain `jobId`, `postId`, `correlationId`
- [ ] Integration tests cover radius edge (inside, inside tolerance, outside), subscription levels, unlimited radius, exclusions, batching (> 50 merchants), deduplication on re-run, and zero matches

---

## Drafted — `[Service]`: New Merchant Post Scan (`NotifyNewMerchantJob`)

**Size:** M

**Brief Description**
A merchant who joins or adds a subscription is notified about already open posts that match them.

**User Story**
As a merchant, I want to see open requests that match me as soon as I finish onboarding or add a subscription so that I do not miss them.

**Description**
Merchant onboarding creates the merchant and branch but no subscriptions, so the scan must also run whenever a subscription is added. Both events write an outbox entry in the same transaction as the change, and the relay (S3) turns it into `NotifyNewMerchantJob`. The job scans `Active`, non-expired posts (whatever their dispatch status) against the merchant's branch and subscriptions using the same matching rule as S3, and records notifications only for posts not yet notified. It is safe to run repeatedly.

**Documentation:** [requirements.md § FR-MATCH-9](../docs/requirements.md) · [architecture.md § Background Jobs](../docs/architecture.md) · [data-model.md](../docs/data-model.md)
**Requirements:** FR-MATCH-2, FR-MATCH-3, FR-MATCH-5, FR-MATCH-9

**Definition of Done**
- [ ] Completing onboarding and adding a subscription each create an outbox entry in the same transaction as the change
- [ ] The job notifies the merchant about every matching `Active`, non-expired post, including unlimited-radius posts and posts still `Pending`
- [ ] Posts the merchant was already notified about are skipped; expired, closed, fulfilled posts and the merchant's own posts are skipped
- [ ] A banned merchant is not notified
- [ ] Running the job twice creates no duplicates
- [ ] Integration tests cover onboarding trigger, subscription trigger, matching/non-matching posts and idempotency

---

## Drafted — `[Service]`: Zero-Match Handling & Post Status Endpoint

**Size:** M

**Brief Description**
The buyer can poll the live status of a post and opt in to a long-lived post when nobody was matched.

**User Story**
As a buyer, I want to see how my request is progressing and extend it when no merchant was found so that I do not lose it too early.

**Description**
Implements `GET /api/posts/{id}/status` and `POST /api/posts/{id}/long-lived` from the [API contract](#api-contract-shared-between-service-and-ui-tickets). The status is derived live from the database on each call (nothing cached): dispatch status, merchants notified, and response-state counts, which stay 0 until merchant responses exist (Phase 5). A zero-match result is reported once dispatch finished with no notified merchants. The long-lived opt-in extends the expiry of an eligible post to 14 days from now (configurable) and marks it long-lived.

**Documentation:** [requirements.md](../docs/requirements.md) · [architecture.md § Post notification dispatch status, Status panel counts](../docs/architecture.md) · [data-model.md § Posts](../docs/data-model.md)
**Requirements:** FR-POST-7, FR-POST-10, FR-MATCH-8

**Definition of Done**
- [ ] The status endpoint returns `Pending` with the current count while matching is running, and `Dispatched` with the final count afterwards
- [ ] `isZeroMatch` is true exactly when the post is `Dispatched` and nobody was notified
- [ ] Response-state counts are present and 0 until responses exist
- [ ] Other buyers' posts return `404`
- [ ] The long-lived opt-in extends `ExpiresAt` to the configured duration from now and sets the long-lived flag
- [ ] The opt-in is rejected with `409` for posts that are not `Active`, not `Dispatched`, urgent, already long-lived, or have at least one matched merchant
- [ ] Integration tests cover each status/eligibility case

---

## Drafted — `[UI]`: Post Creation Form

**Size:** M

**Brief Description**
A buyer can create a request with location, radius, category, tag and optional urgency.

**User Story**
As a buyer, I want to describe what I am looking for and where so that merchants nearby can respond.

**Description**
Rebuilds the current placeholder `/requests/new` page against the [API contract](#api-contract-shared-between-service-and-ui-tickets). The location form is always shown with the buyer's saved locations listed next to it; picking one or using the form fills the coordinates (only one source is active at a time). Radius offers presets (5 / 10 / 25 / 50 km), a custom value, and "unlimited". Category and tag are two dependent selectors from the catalogue (disabled entries are not offered; both required). The buyer can mark the request as urgent and choose a deadline date and time (1–72 h ahead). After a successful creation with a form-entered location the buyer is offered to save it under a name, then lands on the new request. Until the Service ticket lands, the UI works against mocked responses.

**Documentation:** [requirements.md § FR-POST](../docs/requirements.md) · [design-decisions.md §1](../docs/design-decisions.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-POST-1, FR-POST-2, FR-POST-6, FR-LOC-2

**Definition of Done**
- [ ] The form has location (form + saved list), radius, category, tag, title, description, and urgency with deadline; all labels are translated
- [ ] Client-side validation mirrors the contract (required fields, radius > 0, tag from the chosen category, deadline within 1–72 h) and shows field errors; server `400` errors are shown too
- [ ] Choosing a saved location and using the location form are mutually exclusive and clear each other
- [ ] "Unlimited" sends a null radius; the custom radius accepts any value > 0
- [ ] Disabled categories/tags are not selectable
- [ ] After creating a post with a form-entered location a dialog offers to save it with a name; declining or a save error does not block navigation to the new request
- [ ] The form works on mobile and desktop and is keyboard accessible
- [ ] Unit tests cover validation and radius/urgency helpers; an end-to-end test creates a request against the running API

---

## Drafted — `[UI]`: Request List, Detail, Status Panel & Zero-Match

**Size:** L

**Brief Description**
A buyer can browse their requests, follow live matching status, close or fulfil a request and extend a request nobody was found for.

**User Story**
As a buyer, I want to follow my requests and know how many merchants were notified so that I can decide what to do next.

**Description**
Replaces the empty `/requests` list and adds a detail page against the [API contract](#api-contract-shared-between-service-and-ui-tickets). The list has "Active" and "Ended" tabs; each card shows status, number of notified merchants, category and tag, remaining time and urgent / long-lived markers. The detail page shows the request and the existing status panel, refreshed by polling (every 5 s while matching is pending, then every 30 s) until real-time updates arrive in Phase 6. The buyer can mark an active request as found or close it, each after a confirmation; both actions disappear once the request is no longer active. When nobody was matched on an eligible request, a popup offers to extend it to 14 days; dismissing it is remembered locally so it is not shown repeatedly. Sections for responses and chat stay empty placeholders until Phase 5. The buyer home page shows the real requests and status panel while activity and chats stay mocked/empty until Phase 5; the unused budget field is removed.

**Documentation:** [requirements.md § FR-POST, FR-MATCH](../docs/requirements.md) · [architecture.md § Post notification dispatch status](../docs/architecture.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-POST-4, FR-POST-7, FR-POST-8, FR-POST-9, FR-POST-10, FR-MATCH-8

**Definition of Done**
- [ ] `/requests` lists the buyer's requests in "Active" and "Ended" tabs with the card details above and a useful empty state
- [ ] The detail page shows request details and the status panel with notified count, and shows "looking for merchants" while matching is pending
- [ ] The status refreshes automatically (5 s while pending, 30 s afterwards) and stops when leaving the page
- [ ] "Found" and "Close" ask for confirmation, update the request and disappear when the request is not active; a `409` is handled with a clear message
- [ ] The zero-match popup appears only for eligible requests, extends the request on confirmation (the badge and new expiry are shown), and does not reappear after being dismissed
- [ ] The buyer home page uses real requests and status panel data; the `budget` field no longer exists in types or mock data
- [ ] Loading, empty and error states exist for list and detail; the pages work on mobile and desktop
- [ ] Unit tests cover polling/status mapping and eligibility helpers; end-to-end tests cover list, detail, close/fulfil and the zero-match popup against the running API
