# Phase 5 — Merchant Response & Chat — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review) · `Ready` (approved) · `#<n>` (GitHub issue created)

**Deliverable:** A merchant can respond to a post, a chat thread opens, and the full core loop works without real-time (per [planning.md](../docs/planning.md)).

**Requirements in scope:** FR-RESP-1..5, FR-CHAT-1..8, FR-FEED-1..4 (FR-FEED-5/6 are Phase 6), FR-POST-9.

---

## Planning areas

| # | Area | Status |
|---|---|---|
| A | Response model and state machine (incl. status-panel counts) | Decided |
| B | Merchant feed read endpoint (pagination, ordering, filters) | Decided |
| C | Chat REST contract without real-time (threads, messages, unread, inbox) | Decided |
| D | Image attachments | Decided |
| E | Shared API contract (Service ↔ UI) | Decided |
| F | Ticket split and sizing | Decided |

**Gaps found in [planning.md](../docs/planning.md) for Phase 5** (to be covered by tickets): no merchant feed read endpoint (FR-FEED-1..4), no merchant-side post detail, no buyer-side list of responses and no response counts for the status endpoint, no thread list (inbox) with unread counts (FR-CHAT-8).

---

## Epic - [Epic]: Phase 5: Merchant Response & Chat - (GitHub issue not created yet)

**Sub-issues:**

| # | Ticket | Size | Depends on | Status |
|---|---|---|---|---|
| S1 | [Service]: Merchant Response & Thread Creation | L | — | Drafted |
| S2 | [Service]: Merchant Feed | L | S1 | Drafted |
| S3 | [Service]: Buyer Responses & Status Counts | M | S1, S4 | Drafted |
| S4 | [Service]: Chat Threads & Messages | L | S1 | Drafted |
| S5 | [Service]: Chat Image Attachments | M | S4 | Drafted |
| U1 | [UI]: Merchant Feed on Real API | M | contract S2 | Drafted |
| U2 | [UI]: Merchant Post Detail & Response | M | contract S1, S2 | Drafted |
| U3 | [UI]: Buyer Responses & Recent Chats | M | contract S3, S4 | Drafted |
| U4 | [UI]: Chat Inbox & Thread View | L | contract S4 | Drafted |
| U5 | [UI]: Chat Image Attachments | S | contract S5 | Drafted |

Implementation order: S1 → (S2, S4 in parallel) → (S3, S5). UI tickets start in parallel on mocks and switch to the real API when the matching Service ticket has landed.

---

## Decisions (agreed with the architect)

### Area A — Response model and state machine

| # | Decision |
|---|---|
| A1 | One `MerchantResponse` per `(PostId, MerchantId)`; a change of state is an upsert with `UpdatedAt`. Any transition between the four states (`CantHelp`, `MayHaveIt`, `HaveIt`, `CanOrderIt`) is allowed while the post is `Active` ([FR-RESP-3](../docs/requirements.md)). No history table. |
| A2 | Changing a positive state to `CantHelp` keeps the `ChatThread` and its history; the merchant sees the post archived, the buyer still sees the thread. |
| A3 | Status counts: `checking` = notified − responded (notified merchants who have not responded yet); `haveIt`, `mayHaveIt`, `canOrderIt`, `cannotHelp` come from the response states. `GET /api/posts/{id}/status` starts returning them (today they are always 0). |
| A4 | Race with closing: the response is written in a transaction that re-checks `Status = Active` and `ExpiresAt`; otherwise `409 Conflict` (`post_not_active`) and the UI shows an error notification ([FR-RESP-4/5](../docs/requirements.md)). |
| A5 | Only a merchant who was notified about the post (a `PostNotifications` row for the merchant) can respond; everyone else gets `404`. Banned merchants cannot respond. |
| A6 | The first positive response creates the `ChatThread` in the same transaction; creation is idempotent thanks to a unique `(PostId, MerchantId)`. |
| A7 | A response carries only the state — no free-text note (communication goes through chat). |
| A8 | The buyer is notified when a merchant responds ([FR-NOTIF-3](../docs/requirements.md)). Phase 5 has no real-time or push, so the response service only leaves a `TODO(P6)` at the commit point; the in-app event goes out in Phase 6 (`INotificationChannel`) and Web Push in Phase 7. Tracked in the Phase 6 task list in [planning.md](../docs/planning.md). |

### Area B — Merchant feed read endpoint

| # | Decision |
|---|---|
| B1 | The feed is built from the merchant's `PostNotifications` rows joined with `Posts` (`Active`, not expired) and `MerchantResponses` (FR-FEED-1). No re-running of matching on read. |
| B2 | Cursor pagination: `{ items, nextCursor }`. Cursor is `(isUrgent, createdAt, id)` for `newest` and `(distanceKm, id)` for `nearest`. |
| B3 | Filters and sorting are server-side: `tab` (`new` / `responded` / `all`), `categoryId`, `maxDistanceKm`, `sort` (`newest` / `nearest`). |
| B4 | `GET /api/merchant/feed/summary` returns `{ newCount, respondedCount }` (tab badges and navigation badge). |
| B5 | Each item carries `distanceKm` from the merchant branch (PostGIS) and `buyerRadiusKm`. `budget` is dropped from the contract and the UI feed model. |
| B6 | `GET /api/merchant/feed/{postId}` returns the post and the merchant's own response; `404` if the merchant was not notified. Closed, fulfilled and expired posts remain viewable but cannot be answered. |
| B7 | Default order (FR-FEED-3): urgent first, then newest first within each group. |
| B8 | The buyer is shown by name only; `buyerVerified` is dropped from the contract (no verified status exists in the user model). |

### Area C — Chat REST contract (no real-time)

| # | Decision |
|---|---|
| C1 | One endpoint set for both roles, authorised by thread participation: `GET /api/chat/threads` (inbox: `lastMessage`, `unreadCount`, `isLocked`, post and counterpart info), `GET /api/chat/threads/{id}`, `GET /api/chat/threads/{id}/messages`, `POST /api/chat/threads/{id}/messages`, `POST /api/chat/threads/{id}/read`, `GET /api/chat/unread-count`. |
| C2 | The inbox is filtered by the caller's role from the token: a buyer sees threads of their posts, a merchant sees their own threads. |
| C3 | Unread tracking via `BuyerLastReadAt` / `MerchantLastReadAt` on `ChatThread`; `unread` = counterpart messages newer than the caller's `LastReadAt`; `POST .../read` sets it to now. No per-message read flag. |
| C4 | History: cursor `before`, default limit 30, returned in ascending time order; opening a thread loads the latest page, older pages load on scroll up. |
| C5 | Until Phase 6 the UI polls: `after=<lastMessageId>` every few seconds only while the chat view is open and the tab is active; inbox and badge refresh on entry and about every 30 s. Phase 6 swaps polling for SignalR; the REST contract stays. |
| C6 | Sending to a locked thread returns `403` `thread_locked` (FR-CHAT-7). Threads stay writable after the post is closed/fulfilled/expired (FR-CHAT-6). Validation: `body` up to 2000 characters, and a message needs a body or an attachment. Rate limiting only if the infrastructure already exists; otherwise out of Phase 5. |
| C7 | `GET /api/posts/{id}/responses` (buyer) lists merchants with a positive response: shop name, state, `threadId`, `unreadCount`. It feeds the post-detail responses section and the buyer home "recent chats" panel. `CantHelp` merchants appear only in the status counts (A3). |
| C8 | `IsLocked` is a stored flag (not computed on every read). Setting it is part of the ban cascade in Phase 8 — see D5. |

### Area D — Image attachments

| # | Decision |
|---|---|
| D1 | Upload goes through the API: `POST /api/chat/threads/{id}/messages` accepts `multipart/form-data` (`body`, `image`). The backend validates, stores the object in S3, then creates the message, so message and attachment are atomic. No presigned PUT. |
| D2 | The bucket is private. Messages carry `attachmentUrl`, a short-lived presigned GET (about 15 min) generated on every read after the participation check. |
| D3 | JPEG, PNG and WebP only (no GIF/SVG); type verified by magic bytes; one image per message; limit `Chat:MaxAttachmentBytes`, default 5 MB. Errors: `413` `attachment_too_large`, `415` `unsupported_attachment_type`. Client-side resizing is out of Phase 5. |
| D4 | New `IObjectStorage` (put, presigned GET, delete) implemented on AWSSDK.S3, configured by endpoint, bucket and keys; the same code serves MinIO and AWS (NFR-DEV-2). Key: `chat/{threadId}/{messageId}.{ext}`. Bucket is auto-created in dev. Note: `Gdzie.Kupic.Storage` is the EF/PostgreSQL layer, so the object-storage abstraction is new code. |
| D5 | The ban action itself arrives in Phase 8, so Phase 5 delivers (a) enforcement: sending to a thread with `IsLocked = true` returns `403` `thread_locked`, and (b) a domain operation in `IChatStorage` (e.g. `SetLockForUserAsync`) that locks all of a user's threads and, on unban, recomputes the lock (`IsLocked` = any participant still banned). Phase 8 calls it inside the ban/unban transaction. A banned merchant cannot respond or write anyway: the auth middleware rejects every request of a banned account. |
| D6 | Attachments live as long as the thread; retention and clean-up are out of Phase 5. |
| D7 | A simple bucket reachability health check is added with the attachments ticket. |

### Area E — Shared API contract

| # | Decision |
|---|---|
| E1 | The contract below is frozen in this file; DTOs live in `Gdzie.Kupic.API.Contract`; errors use `ProblemDetails` with a machine-readable `code` (`post_not_active`, `thread_locked`, `attachment_too_large`, `unsupported_attachment_type`). The UI branches on `code`, never on message text. |
| E2 | Setting the response is an idempotent `PUT` (upsert, A1). |
| E3 | `city` is dropped from the feed item (distance only). `buyerName` is the first name only. |
| E4 | UI tickets start in parallel on mocks and switch to the real API through the existing `merchantFeedMock` / `buyerHomeMock` flags plus a new `chatMock`; each UI ticket depends on the matching Service endpoints only for the switch. |

### Area F — Ticket split

| # | Decision |
|---|---|
| F1 | Five Service tickets (S1–S5) and five UI tickets (U1–U5) as in the table above; S3 stays separate from S1 to keep S1 at size L. |
| F2 | The core-loop end-to-end test against the real backend (post → response → chat) is an acceptance criterion of U4, not a separate ticket. |
| F3 | D5 revised: the ban cascade is Phase 8; Phase 5 ships enforcement and the lock operation only. |

---

## Assumptions & open questions

1. **Out of scope for Phase 5:** real-time delivery (Phase 6, incl. the buyer notification `TODO(P6)`), Web Push / email (Phase 7), the ban action and its cascade (Phase 8), FR-FEED-5/6 (removal and banner when a post closes while the merchant has it open — Phase 6), attachment retention/clean-up, client-side image resizing, chat rate limiting.
2. A closed, fulfilled or expired post disappears from the merchant feed (B1) but stays reachable through its chat thread and by direct link (B6).
3. `ResponseItem.unreadCount` (S3) reads the unread tracking delivered in S4, hence S3 depends on S4.
4. Documentation to update alongside the tickets: [data-model.md](../docs/data-model.md) (`ChatThread` read-marker columns, final `MerchantResponses` shape) and [planning.md](../docs/planning.md) task status.

---

## API contract (shared between Service and UI tickets)

`ResponseState` = `CantHelp | MayHaveIt | HaveIt | CanOrderIt` (string). Unauthenticated: `401`. Wrong role: `403`.

**Merchant** (role Merchant)

| Method + route | Request | Response |
|---|---|---|
| `GET /api/merchant/feed?tab=new\|responded\|all&categoryId&maxDistanceKm&sort=newest\|nearest&cursor&limit` | `limit` default 20, max 50 | `200` `{ items: FeedItem[], nextCursor \| null }` |
| `GET /api/merchant/feed/summary` | — | `200` `{ newCount, respondedCount }` |
| `GET /api/merchant/feed/{postId}` | — | `200` `FeedItem & { threadId \| null }` · `404` |
| `PUT /api/merchant/feed/{postId}/response` | `{ state }` | `200` `{ state, threadId \| null, updatedAt }` · `404` (not notified / banned) · `409` `post_not_active` |

`FeedItem`: `{ id, title, description | null, category: { id, name }, tag: { id, name }, distanceKm, buyerRadiusKm | null, buyerName, isUrgent, urgentDeadline | null, expiresAt, status, createdAt, myResponse: ResponseState | null }`

**Buyer** (role Buyer)

| Method + route | Response |
|---|---|
| `GET /api/posts/{id}/status` (existing) | counts become real (A3) |
| `GET /api/posts/{id}/responses` | `200` `ResponseItem[]` = `{ merchantId, shopName, state, threadId, unreadCount, updatedAt }`, positive responses only (C7) · `404` |

**Chat** (both roles, authorised by thread participation)

| Method + route | Request | Response |
|---|---|---|
| `GET /api/chat/threads?cursor&limit` | — | `200` `{ items: ThreadSummary[], nextCursor }` |
| `GET /api/chat/threads/{id}` | — | `200` `ThreadSummary` · `404` |
| `GET /api/chat/threads/{id}/messages?before\|after&limit` | `limit` default 30 | `200` `{ items: Message[], hasMore }` (ascending by time) |
| `POST /api/chat/threads/{id}/messages` | `multipart/form-data`: `body?`, `image?` | `201` `Message` · `400` empty or too long · `403` `thread_locked` · `404` · `413` · `415` |
| `POST /api/chat/threads/{id}/read` | — | `204` |
| `GET /api/chat/unread-count` | — | `200` `{ count }` |

`ThreadSummary`: `{ id, post: { id, title, status }, counterpart: { id, displayName }, lastMessage: { preview, createdAt, isMine } | null, unreadCount, isLocked, createdAt }` — `counterpart` is the shop name for a buyer and the buyer's first name for a merchant.

`Message`: `{ id, threadId, senderId, isMine, body | null, attachmentUrl | null, createdAt }`

---

## Drafted — `[Service]`: Merchant Response & Thread Creation

**Size:** L

**Brief Description**
A notified merchant can answer a post with one of four states; the first positive answer opens a chat thread.

**User Story**
As a merchant, I want to tell a buyer whether I have what they are looking for so that we can talk about it.

**Description**
Introduces the `MerchantResponse` entity (one row per post and merchant, state plus `UpdatedAt`) with its migration, and the `PUT /api/merchant/feed/{postId}/response` endpoint from the [API contract](#api-contract-shared-between-service-and-ui-tickets). The call is an upsert: any change between the four states is allowed while the post is `Active` and not past `ExpiresAt`; the check and the write happen in one transaction, so a response that races with closing is rejected with `409` `post_not_active`. Only a merchant with a `PostNotifications` row for the post can respond (everyone else gets `404`). The first positive state (`MayHaveIt`, `HaveIt`, `CanOrderIt`) creates the `ChatThread` in the same transaction, idempotently thanks to a unique `(PostId, MerchantId)`; switching to `CantHelp` keeps the thread. At the commit point a `TODO(P6)` marks where the buyer notification (FR-NOTIF-3) will be raised. The `Gdzie.Kupic.Chat` module installer and the `ChatThread` entity (re-linked to the merchant) are brought up as needed.

**Documentation:** [requirements.md § FR-RESP, FR-CHAT](../docs/requirements.md) · [data-model.md § MerchantResponses, ChatThreads](../docs/data-model.md) · [architecture.md](../docs/architecture.md)
**Requirements:** FR-RESP-1, FR-RESP-2, FR-RESP-3, FR-RESP-4, FR-RESP-5, FR-CHAT-1, FR-CHAT-2, FR-CHAT-3

**Definition of Done**
- [ ] `MerchantResponses` exists via a migration with a unique `(PostId, MerchantId)` and `UpdatedAt`; `ChatThreads` has a unique `(PostId, MerchantId)`
- [ ] `PUT .../response` creates or updates the merchant's response and returns `state`, `threadId` (or `null`) and `updatedAt`
- [ ] A merchant who was not notified about the post (or an unknown post) gets `404`; a banned account is rejected by the existing auth middleware
- [ ] A post that is not `Active` or is past `ExpiresAt` yields `409` with `code = post_not_active`; the check and the write are in one transaction
- [ ] The first positive response creates exactly one `ChatThread`; repeated or concurrent calls never create a second one
- [ ] Switching from a positive state to `CantHelp` keeps the thread and its messages
- [ ] A `TODO(P6)` at the commit point refers to the buyer notification for FR-NOTIF-3
- [ ] Unit tests cover the state transitions and the active/expiry rule; integration tests cover the endpoint (ownership, 404/409, thread creation, idempotency, concurrent first response)

---

## Drafted — `[Service]`: Merchant Feed

**Size:** L

**Brief Description**
A merchant can browse the posts they were notified about, filtered and ordered, and open a single post.

**User Story**
As a merchant, I want to see the requests that match my shop, nearest or newest first, so that I can respond to the ones I can help with.

**Description**
Implements `GET /api/merchant/feed`, `GET /api/merchant/feed/summary` and `GET /api/merchant/feed/{postId}` from the [API contract](#api-contract-shared-between-service-and-ui-tickets). The feed is built from the merchant's `PostNotifications` joined with `Posts` (`Active`, not expired) and the merchant's own `MerchantResponses`. The `tab` parameter splits unanswered (`new`) from answered (`responded`, incl. `CantHelp`); `categoryId` and `maxDistanceKm` filter; `sort` is `newest` (urgent first, then newest first within each group) or `nearest`. Pagination is by cursor (`(isUrgent, createdAt, id)` or `(distanceKm, id)`). Each item carries the distance from the merchant branch computed with PostGIS, the buyer's radius, the buyer's first name and the merchant's own response. `summary` returns the two counts for the tab and navigation badges. The detail endpoint also returns closed, fulfilled and expired posts the merchant was notified about, together with the thread id when one exists.

**Documentation:** [requirements.md § FR-FEED](../docs/requirements.md) · [data-model.md § Posts, PostNotifications, MerchantResponses](../docs/data-model.md) · [architecture.md](../docs/architecture.md)
**Requirements:** FR-FEED-1, FR-FEED-2, FR-FEED-3, FR-FEED-4

**Definition of Done**
- [ ] The feed lists only `Active`, non-expired posts the merchant was notified about; posts of other merchants never appear
- [ ] `tab`, `categoryId`, `maxDistanceKm` and `sort` work and can be combined
- [ ] Default order is urgent first, then newest first; `nearest` orders by distance; both are stable across pages via the cursor and contain no duplicates or gaps when new posts arrive
- [ ] `limit` defaults to 20 and is capped at 50; the last page returns `nextCursor = null`
- [ ] Items contain `distanceKm`, `buyerRadiusKm`, `buyerName` (first name only), `myResponse` and no budget or verification fields
- [ ] `summary` returns correct `newCount` and `respondedCount`
- [ ] The detail endpoint returns closed/fulfilled/expired posts, includes `threadId` when a thread exists, and returns `404` for posts the merchant was not notified about
- [ ] Queries use indexes suited to the feed (no per-row distance computation over unrelated posts); integration tests cover filters, ordering, pagination and ownership

---

## Drafted — `[Service]`: Buyer Responses & Status Counts

**Size:** M

**Brief Description**
The buyer can see real response counts for a post and a list of merchants who can help.

**User Story**
As a buyer, I want to see who responded to my request so that I can start a conversation with the merchants who have what I need.

**Description**
Makes the existing `GET /api/posts/{id}/status` return real counts: `haveItCount`, `mayHaveItCount`, `canOrderItCount` and `cannotHelpCount` come from `MerchantResponses`; `checkingCount` is the notified merchants who have not responded yet. Adds `GET /api/posts/{id}/responses`, which lists only merchants with a positive response (shop name, state, `threadId`, `unreadCount`, `updatedAt`); merchants who answered `CantHelp` appear in the counts only. The `unreadCount` comes from the unread tracking of the chat ticket (S4). Both endpoints return `404` for posts of other buyers.

**Documentation:** [requirements.md § FR-RESP, FR-CHAT-8](../docs/requirements.md) · [architecture.md § Status panel counts](../docs/architecture.md) · [data-model.md](../docs/data-model.md)
**Requirements:** FR-RESP-1, FR-CHAT-3, FR-CHAT-8, FR-POST-9

**Definition of Done**
- [ ] The status endpoint returns correct counts after responses are created, changed (a changed state moves the count) and for posts with no responses
- [ ] `checkingCount` equals notified minus responded and never goes negative
- [ ] `GET /api/posts/{id}/responses` lists only positive responses with `threadId` and the caller's `unreadCount`; `CantHelp` merchants are not listed
- [ ] Responses are ordered by `updatedAt` descending
- [ ] Other buyers' posts return `404` on both endpoints
- [ ] Integration tests cover each state, a state change and ownership

---

## Drafted — `[Service]`: Chat Threads & Messages

**Size:** L

**Brief Description**
Buyer and merchant can list their threads, read the history and exchange text messages, with unread counts.

**User Story**
As a buyer or merchant, I want to talk to the other side about a request so that we can agree on the details.

**Description**
Implements the chat endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets) (except the image part, see S5): inbox `GET /api/chat/threads`, `GET /api/chat/threads/{id}`, history `GET .../messages` with `before` / `after` cursors and a default page of 30 returned in ascending order, `POST .../messages` for text, `POST .../read` and `GET /api/chat/unread-count`. Access is limited to the two participants (`404` for anyone else); the inbox is filtered by the caller's role. Unread tracking uses `BuyerLastReadAt` / `MerchantLastReadAt` columns on `ChatThread` (migration); `unreadCount` is the number of the counterpart's messages newer than the caller's marker. Messages are plain text up to 2000 characters. Threads stay writable after the post closes, is fulfilled or expires; a thread with `IsLocked = true` rejects new messages with `403` `thread_locked`. `IChatStorage` also gets a domain operation (e.g. `SetLockForUserAsync`) that locks all threads of a user and, on unban, recomputes the lock; it is called by the ban cascade in Phase 8. `IChatChannel` is left for Phase 6.

**Documentation:** [requirements.md § FR-CHAT](../docs/requirements.md) · [data-model.md § ChatThreads, ChatMessages](../docs/data-model.md) · [architecture.md](../docs/architecture.md)
**Requirements:** FR-CHAT-1, FR-CHAT-3, FR-CHAT-5, FR-CHAT-6, FR-CHAT-7, FR-CHAT-8

**Definition of Done**
- [ ] A migration adds `BuyerLastReadAt` and `MerchantLastReadAt` to `ChatThreads` (and an index on messages by thread and time); `docs/data-model.md` is updated
- [ ] The inbox returns the caller's threads only, newest activity first, with `lastMessage`, `unreadCount`, `isLocked`, post and counterpart info (shop name for buyers, buyer first name for merchants) and cursor pagination
- [ ] History returns pages in ascending order, loads older pages with `before` and newer ones with `after`, and reports `hasMore`
- [ ] Sending text creates a persisted message; empty or over-long bodies return `400`
- [ ] A user who is not a participant gets `404` on every thread endpoint
- [ ] `POST .../read` moves the caller's marker and `unreadCount` / `unread-count` reflect it for both roles
- [ ] Sending to a locked thread returns `403` `thread_locked`; closed, fulfilled and expired posts do not block messages
- [ ] `SetLockForUserAsync` (or equivalent) locks a user's threads and recomputes the lock on unban, covered by tests
- [ ] Unit and integration tests cover access control, pagination, unread counts and locking

---

## Drafted — `[Service]`: Chat Image Attachments

**Size:** M

**Brief Description**
A participant can attach one image to a chat message; images are stored in S3-compatible storage and served privately.

**User Story**
As a buyer or merchant, I want to send a photo so that the other side can see exactly what I mean.

**Description**
Extends `POST /api/chat/threads/{id}/messages` to accept `multipart/form-data` with `body` and `image`. The backend validates size (configurable `Chat:MaxAttachmentBytes`, default 5 MB) and type (JPEG, PNG or WebP, checked by magic bytes, one image per message), stores the object in a private bucket under `chat/{threadId}/{messageId}.{ext}` and then creates the message, so a message never points to a missing object. A new `IObjectStorage` abstraction (put, presigned GET, delete) is implemented with the AWS S3 SDK and configured by endpoint, bucket and credentials, so MinIO in local development and AWS in production use the same code; the bucket is created automatically in development. Messages expose `attachmentUrl`, a short-lived presigned GET generated on every read after the participation check. Oversized files return `413` `attachment_too_large`, other types `415` `unsupported_attachment_type`. A simple bucket reachability check is added to the health checks.

**Documentation:** [requirements.md § FR-CHAT-4, NFR-DEV-2](../docs/requirements.md) · [architecture.md § File storage](../docs/architecture.md) · [local-dev.md](../docs/local-dev.md) · [observability.md](../docs/observability.md)
**Requirements:** FR-CHAT-4, NFR-DEV-2

**Definition of Done**
- [ ] A message can carry text, an image, or both; an empty message is still rejected
- [ ] JPEG, PNG and WebP are accepted; GIF, SVG and files whose content does not match the declared type are rejected with `415`
- [ ] Files above the configured limit are rejected with `413`; the limit is configurable and defaults to 5 MB
- [ ] The object is stored in a private bucket under the documented key; if storing fails no message is created
- [ ] `attachmentUrl` is a presigned URL that expires (about 15 minutes) and is returned only to the thread's participants
- [ ] `IObjectStorage` works unchanged against MinIO (Docker stack) and an AWS-style endpoint through configuration; the dev bucket is created on startup
- [ ] The health check reports bucket reachability; `docs/local-dev.md` documents the settings
- [ ] Integration tests (against MinIO or a test double) cover valid upload, oversize, wrong type and access control

---

## Drafted — `[UI]`: Merchant Feed on Real API

**Size:** M

**Brief Description**
The merchant feed shows real requests with server-side filters, sorting and infinite scroll.

**User Story**
As a merchant, I want a fast, filterable list of requests that match my shop so that I can find the ones worth answering.

**Description**
Replaces the mock data behind `/feed` with the feed endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). Tabs (new / responded / all), category, maximum distance and sorting are sent to the server; the list loads further pages by cursor while scrolling and resets on any filter change. The tab and navigation badges use the `summary` endpoint. The feed model drops `budget`, `buyerVerified` and `city` and shows only the distance and the buyer's first name. Urgent requests keep their marker and deadline. Mocks stay available behind the existing `merchantFeedMock` flag so work can start before the Service ticket lands.

**Documentation:** [requirements.md § FR-FEED](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-FEED-1, FR-FEED-2, FR-FEED-3, FR-FEED-4

**Definition of Done**
- [ ] `/feed` loads real requests; tab, category, distance and sort changes reload from the server and reset the cursor
- [ ] Scrolling to the end loads the next page without duplicates and stops when `nextCursor` is `null`
- [ ] Tab badges and the navigation badge show the counts from `summary`
- [ ] Cards show distance, the buyer's first name, urgency and the merchant's own response; `budget`, `buyerVerified` and `city` no longer exist in types or mocks
- [ ] Loading, empty and error states exist; the page works on mobile and desktop
- [ ] The mock mode still works behind `merchantFeedMock`
- [ ] Unit tests cover the store and query-building helpers; end-to-end tests cover filters, sorting and infinite scroll

---

## Drafted — `[UI]`: Merchant Post Detail & Response

**Size:** M

**Brief Description**
A merchant can open a request and respond with one of four states, with correct handling of closed posts and races.

**User Story**
As a merchant, I want to answer a request in one tap and change my answer later so that the buyer knows whether I can help.

**Description**
Rebuilds `/feed/[id]` and `ResponseButtons` on the detail and response endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). The page shows the request (category, tag, distance, buyer's first name, urgency, expiry) and the four response buttons in the order Have it / May have it / Can order it / Can't help, with the current choice highlighted; a choice can be changed any time while the request is active. A positive response shows a link to the chat thread. When the request is not active (closed, fulfilled, expired) the buttons are disabled and an explanation is shown, while the thread link stays available. A `409` with `code = post_not_active` from a response that races with closing shows an error notification and refreshes the page state. Mocks remain behind `merchantFeedMock` until the Service tickets land.

**Documentation:** [requirements.md § FR-RESP](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-RESP-1, FR-RESP-2, FR-RESP-3, FR-RESP-4, FR-RESP-5, FR-CHAT-3

**Definition of Done**
- [ ] The detail page loads from the real endpoint; a request the merchant was not notified about shows a not-found state
- [ ] Choosing a state calls `PUT .../response` once per action (no double submit), shows the new state and updates the feed card
- [ ] A positive state shows a link to the thread when `threadId` is returned
- [ ] For a non-active request the buttons are disabled with an explanation and the thread link is kept
- [ ] A `post_not_active` error shows an error notification and re-synchronises the page
- [ ] Loading, error and keyboard-accessible states exist; the page works on mobile and desktop
- [ ] Unit tests cover the state mapping; end-to-end tests cover responding, changing the answer, the closed-post state and the race error

---

## Drafted — `[UI]`: Buyer Responses & Recent Chats

**Size:** M

**Brief Description**
The buyer sees real response counts and the merchants who can help, with a path into the chat.

**User Story**
As a buyer, I want to see who responded to my request so that I can open a conversation with the right merchant.

**Description**
Completes the buyer side against the status and responses endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). The status panel on the request detail and on the buyer home shows the real counts (checking, have it, may have it, can order it, can't help). The placeholder response section on the request detail becomes a list of positive responses (shop name, state badge, unread count) that links to the chat thread. The "recent chats" panel on the buyer home uses real threads instead of mock data. Existing polling of the status continues until Phase 6; the responses list refreshes on the same schedule.

**Documentation:** [requirements.md § FR-RESP, FR-CHAT-8](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-RESP-1, FR-CHAT-3, FR-CHAT-8, FR-POST-9

**Definition of Done**
- [ ] The status panel shows real counts on the request detail and the buyer home
- [ ] The request detail lists merchants with a positive response, their state and unread count, each linking to its thread
- [ ] `CantHelp` merchants appear only in the counts, not in the list
- [ ] `RecentChats` shows real threads (latest activity first, with unread counts) and a useful empty state
- [ ] The lists refresh on the polling schedule and stop when leaving the page
- [ ] Mocks remain behind the existing flags until the Service tickets land; loading and error states exist; mobile and desktop work
- [ ] Unit tests cover mapping helpers; end-to-end tests cover counts, the responses list and the link to chat

---

## Drafted — `[UI]`: Chat Inbox & Thread View

**Size:** L

**Brief Description**
Buyers and merchants have a chat inbox and a message view with history, sending and unread indicators; the core loop works end to end.

**User Story**
As a buyer or merchant, I want to chat about a request and see new messages so that we can agree on the purchase.

**Description**
Adds the chat views against the chat endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets), for both roles. The inbox lists the caller's threads (counterpart, request title, last message preview, unread count, locked marker) with cursor pagination. The thread view shows the request header and the message history: the latest page first, older pages loading when scrolling up, own and incoming messages visually distinct. Sending a text message is optimistic with a failure state; a locked thread disables the composer and explains why. Opening a thread marks it read. Until real-time arrives in Phase 6 the view polls for new messages (`after=<lastMessageId>`, every few seconds, only while open and the tab is active) and the inbox and the navigation unread badge refresh on entry and about every 30 s. A `chatMock` flag provides mock data until the Service ticket lands. Acceptance includes an end-to-end test against the real backend of the full loop: post, merchant response, chat both ways.

**Documentation:** [requirements.md § FR-CHAT](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-CHAT-3, FR-CHAT-5, FR-CHAT-6, FR-CHAT-7, FR-CHAT-8

**Definition of Done**
- [ ] An inbox page for buyers and one for merchants (or one shared page by role) lists threads with unread counts and a locked marker, and has a useful empty state
- [ ] The thread view loads the latest messages, loads older pages when scrolling up and keeps the scroll position
- [ ] Sending text works with an optimistic message and a retry on failure; empty and over-long input is prevented
- [ ] A locked thread disables the composer and shows an explanation; threads of closed posts stay writable
- [ ] Opening a thread marks it read; the navigation badge and inbox counts update
- [ ] Polling runs only while the view is open and the tab is active and stops on leave; the inbox and badge refresh about every 30 s
- [ ] `chatMock` works until the Service ticket lands; chat views work on mobile and desktop and are keyboard accessible
- [ ] Unit tests cover the store, pagination and polling helpers; an end-to-end test on the real backend covers post → response → chat in both directions

---

## Drafted — `[UI]`: Chat Image Attachments

**Size:** S

**Brief Description**
A chat participant can attach an image to a message and see images in the conversation.

**User Story**
As a buyer or merchant, I want to send and view photos in the chat so that we can identify the product quickly.

**Description**
Extends the thread view of the chat ticket (U4) with images, against the attachment behaviour in the [API contract](#api-contract-shared-between-service-and-ui-tickets). The composer lets the user pick one image (JPEG, PNG or WebP), shows a preview with a remove action, and sends it as `multipart/form-data` together with optional text. Messages with `attachmentUrl` render the image with a click-to-enlarge view; because the URL expires, the thread view reloads URLs through the normal message fetch. Client-side checks mirror the limits (type and size from the configured maximum) and the server errors `413` `attachment_too_large` and `415` `unsupported_attachment_type` are shown as clear messages.

**Documentation:** [requirements.md § FR-CHAT-4](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-CHAT-4

**Definition of Done**
- [ ] The composer accepts one JPEG, PNG or WebP image, shows a preview and allows removing it before sending
- [ ] Files that are too large or of another type are rejected on the client with a clear message; server `413` and `415` errors show the same messages
- [ ] A message with an image (with or without text) is sent as multipart and appears in the thread
- [ ] Incoming images render in the thread with a larger view on click; a failed or expired image shows a fallback
- [ ] The feature works behind `chatMock` and on the real API; mobile and desktop work
- [ ] Unit tests cover the file validation helper; an end-to-end test sends an image against the running API

---

## Follow-ups for later phases

| Phase | Item |
|---|---|
| 6 | Buyer notification when a merchant responds (FR-NOTIF-3) — `TODO(P6)` in the Phase 5 response service; deliver via `INotificationChannel`. |
| 7 | Same event delivered as Web Push / email fallback. |
