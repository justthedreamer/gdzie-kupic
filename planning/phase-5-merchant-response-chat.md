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
| B | Merchant feed read endpoint (pagination, ordering, filters) | Open |
| C | Chat REST contract without real-time (threads, messages, unread, inbox) | Open |
| D | Image attachments | Open |
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

---

## Follow-ups for later phases

| Phase | Item |
|---|---|
| 6 | Buyer notification when a merchant responds (FR-NOTIF-3) — `TODO(P6)` in the Phase 5 response service; deliver via `INotificationChannel`. |
| 7 | Same event delivered as Web Push / email fallback. |
