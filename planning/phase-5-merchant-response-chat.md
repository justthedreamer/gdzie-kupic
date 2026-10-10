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
| E | Shared API contract (Service ↔ UI) | Open |
| F | Ticket split and sizing | Open |

**Gaps found in [planning.md](../docs/planning.md) for Phase 5** (to be covered by tickets): no merchant feed read endpoint (FR-FEED-1..4), no merchant-side post detail, no buyer-side list of responses and no response counts for the status endpoint, no thread list (inbox) with unread counts (FR-CHAT-8).

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
| C8 | `IsLocked` is set when a participant is banned (not computed on every read), per the ban-lock ticket in [planning.md](../docs/planning.md). |

### Area D — Image attachments

| # | Decision |
|---|---|
| D1 | Upload goes through the API: `POST /api/chat/threads/{id}/messages` accepts `multipart/form-data` (`body`, `image`). The backend validates, stores the object in S3, then creates the message, so message and attachment are atomic. No presigned PUT. |
| D2 | The bucket is private. Messages carry `attachmentUrl`, a short-lived presigned GET (about 15 min) generated on every read after the participation check. |
| D3 | JPEG, PNG and WebP only (no GIF/SVG); type verified by magic bytes; one image per message; limit `Chat:MaxAttachmentBytes`, default 5 MB. Errors: `413` `attachment_too_large`, `415` `unsupported_attachment_type`. Client-side resizing is out of Phase 5. |
| D4 | New `IObjectStorage` (put, presigned GET, delete) implemented on AWSSDK.S3, configured by endpoint, bucket and keys; the same code serves MinIO and AWS (NFR-DEV-2). Key: `chat/{threadId}/{messageId}.{ext}`. Bucket is auto-created in dev. Note: `Gdzie.Kupic.Storage` is the EF/PostgreSQL layer, so the object-storage abstraction is new code. |
| D5 | Banning a user sets `IsLocked = true` on all their threads in the ban transaction. Unbanning recomputes the lock (`IsLocked` = any participant still banned). |
| D6 | Attachments live as long as the thread; retention and clean-up are out of Phase 5. |
| D7 | A simple bucket reachability health check is added with the attachments ticket. |

---

## Follow-ups for later phases

| Phase | Item |
|---|---|
| 6 | Buyer notification when a merchant responds (FR-NOTIF-3) — `TODO(P6)` in the Phase 5 response service; deliver via `INotificationChannel`. |
| 7 | Same event delivered as Web Push / email fallback. |
