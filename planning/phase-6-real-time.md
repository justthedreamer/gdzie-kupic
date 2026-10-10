# Phase 6 — Real-Time (SignalR) — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review) · `Ready` (approved) · `#<n>` (GitHub issue created)

**Deliverable:** Status panel, merchant feed and chat update live over a single SignalR connection; polling remains only as a fallback (per [planning.md](../docs/planning.md)).

**Requirements in scope:** FR-FEED-5, FR-FEED-6, FR-CHAT-4/5 (live delivery), FR-NOTIF-3 (in-app part), status panel live counts ([design-decisions §3](../docs/design-decisions.md)). Web Push / email delivery stays in Phase 7.

---

## Planning areas

| # | Area | Status |
|---|---|---|
| A | Connection topology and hub design | Decided |
| B | Authentication, authorization and groups | Decided |
| C | Event contract and payloads | Decided |
| D | Server-side event sources (where services raise events) | Decided |
| E | UI client, reconnect and polling fallback | Decided |
| F | Ticket split and sizing | Decided |

**Gaps found in the docs** (to be covered by decisions/tickets):

1. [architecture.md §6](../docs/architecture.md) promises one connection per client but its manifest has three hubs (three connections in SignalR).
2. The `AddHub<TInterface, THub>` pattern registers hubs as singletons; SignalR hubs are per-invocation, server-initiated pushes go through `IHubContext<T>`.
3. The buyer status panel has no clear channel interface (planning.md puts it under `IPostFeedChannel`, architecture describes that channel as the merchant feed).
4. [design-decisions §2](../docs/design-decisions.md) says the feed push happens on post creation; since Phase 5 the feed is derived from `PostNotifications`, so `PostAdded` must fire after matching/dispatch, per matched merchant.
5. FR-NOTIF-3 spans Phases 6 (in-app) and 7 (Web Push); `TODO(P6)` in `MerchantResponseService`.

---

## Epic - [Epic]: Phase 6: Real-Time - [#119](https://github.com/justthedreamer/gdzie-kupic/issues/119)

**Sub-issues:**

| # | Ticket | Size | Depends on | Status |
|---|---|---|---|---|
| S1 | [Service]: Realtime Foundation | M | — | [#120](https://github.com/justthedreamer/gdzie-kupic/issues/120) |
| S2 | [Service]: Post Feed & Status Events | L | S1 | [#121](https://github.com/justthedreamer/gdzie-kupic/issues/121) |
| S3 | [Service]: Chat & Notification Events | M | S1 | [#122](https://github.com/justthedreamer/gdzie-kupic/issues/122) |
| U1 | [UI]: Realtime Client & Connection | M | contract S1 | [#123](https://github.com/justthedreamer/gdzie-kupic/issues/123) |
| U2 | [UI]: Live Buyer Status & Responses | S | U1, contract S2 | [#124](https://github.com/justthedreamer/gdzie-kupic/issues/124) |
| U3 | [UI]: Live Merchant Feed & Closed-Post Banner | M | U1, contract S2 | [#125](https://github.com/justthedreamer/gdzie-kupic/issues/125) |
| U4 | [UI]: Live Chat & In-App Notifications | M | U1, contract S3 | [#126](https://github.com/justthedreamer/gdzie-kupic/issues/126) |

Implementation order: S1 → (S2, S3 in parallel). UI tickets start in parallel on mocks (U1 first) and switch to the real hub when the matching Service ticket has landed.

---

## Decisions (agreed with the architect)

### Area A — Connection topology and hub design

| # | Decision |
|---|---|
| A1 | One `AppHub` and one connection per client. Logical separation is done with groups (`post:{id}`, `thread:{id}`, `user:{id}`, merchant feed), not with separate hubs. |
| A2 | The three channel interfaces (`IPostFeedChannel`, `IChatChannel`, `INotificationChannel`) stay in their owning modules without a SignalR dependency. `Gdzie.Kupic.Realtime` implements them with classes that push through `IHubContext<AppHub>`; it remains the only module referencing SignalR. |
| A3 | `RealtimeBuilder.AddHub<TInterface, THub>()` is replaced by `AddChannel<TInterface, TImplementation>()` (constraint `TImplementation : TInterface`, singleton registration); the explicit manifest stays in `Program.cs` together with `app.MapRealtimeHubs()`. |
| A4 | No separate ADR: [architecture.md §6](../docs/architecture.md) and [design-decisions.md](../docs/design-decisions.md) are updated as part of the first Service ticket. |

### Area B — Authentication, authorization and groups

| # | Decision |
|---|---|
| B1 | Per-user addressing only. On connect the server adds the connection to a single group `user:{userId}` derived from the validated JWT; there are no client-callable `Join*` methods and no per-post / per-thread groups. The server resolves recipients from persisted state (post owner, matched merchants via `PostNotifications`, thread participants) and sends to each recipient's `user:{id}` group. The "scoped to post / per-thread channel" wording in [architecture.md §6](../docs/architecture.md) is updated accordingly. |
| B2 | Account status and token expiry are verified only when the connection is established (JWT `OnTokenValidated` incl. `IAccountStatusCache`). No active disconnecting of banned accounts in Phase 6 — an existing connection lives until it drops; events go only to recipients resolved from the database and REST stays protected. Known limitation, revisited with moderation (Phase 8). |
| B3 | JWT is passed as `access_token` query string for `/hubs/*` (`OnMessageReceived`). CORS stays without `AllowCredentials()`; the UI client uses `withCredentials: false`. |

### Area C — Event contract and payloads

| # | Decision |
|---|---|
| C1 | Thin events: the server pushes only a type and identifiers; the client re-fetches current state through the existing REST endpoints (status, responses, feed, thread messages, inbox, unread). No per-recipient payloads (`IsMine`, `DistanceKm`) are built for SignalR; a lost event is harmless because reconnect performs the same refetch. |
| C2 | Event catalogue (camelCase on the wire): `postAdded {postId}` (matched merchant, after dispatch), `postRemoved {postId}` (merchants with a `PostNotifications` row, on Closed / Fulfilled / Expired — FR-FEED-5/6), `postStatusChanged {postId}` (post owner: new/changed response, dispatch completed, status change), `messageReceived {threadId, messageId}` (the other participant), `threadUpdated {threadId}` (participants: thread created, locked — FR-CHAT-7), `notificationRaised {kind, postId?, threadId?}` (in-app notification: merchant response received by buyer, new chat message — FR-NOTIF-3 in-app part). The catalogue may gain fields during implementation. |
| C3 | Event names and payload records live in `API.Contract/Realtime`. Channel interfaces in the modules use plain parameters (e.g. `PostAddedAsync(Guid recipientUserId, Guid postId)`); `Realtime` maps them to the contract. |
| C4 | Events and payloads are documented in UI `docs/api.md` (Realtime section) and in [architecture.md §6](../docs/architecture.md). A breaking change adds a new event instead of altering an existing one. |

### Area D — Server-side event sources

| # | Decision |
|---|---|
| D1 | Events are sent directly after the successful `SaveChangesAsync` of the service/job that changed the state; no outbox. A lost event (crash between commit and send) is healed by the client refetch on reconnect / polling fallback. |
| D2 | A channel call never affects the outcome of the request or job: failures are caught and logged. No Hangfire job per event. |
| D3 | `ExpireOverduePostsAsync` returns the ids of the posts it expired (not just a count); `ExpirePostsJob` then sends `postRemoved` to merchants with a `PostNotifications` row and `postStatusChanged` to the owner. Closed / Fulfilled go through `PostService.EndAsync`. |
| D4 | Channel interfaces take recipient **user** ids. A merchant maps to one or more users through `MerchantAccount (MerchantId, UserId)`, so merchant-addressed events (`postAdded`, `postRemoved`) resolve `merchantId → userIds` in the Marketplace service/job, not in `Realtime`. `postAdded` is raised in `NotifyMerchantsBatchJob` and `NotifyNewMerchantJob` for exactly the newly created notifications. |
| D5 | Phase 6 delivers only the in-app (SignalR) channel. `INotificationDispatcher` (Web Push / email seam) stays no-op until Phase 7; `TODO(P6)` in `MerchantResponseService` becomes a `INotificationChannel` call. Phase 7 may call `INotificationChannel` as one step of the FR-NOTIF-2 hierarchy. |
| D6 | Service tests use a recording fake of each channel interface (like `RecordingNotificationDispatcher`); the hub / `IHubContext` mapping is covered by a separate integration test with a real SignalR client on `WebApplicationFactory`. |

### Area E — UI client, reconnect and polling fallback

| # | Decision |
|---|---|
| E1 | A client-only Nuxt plugin owns the single `HubConnection` (`@microsoft/signalr`), created after authentication: `accessTokenFactory` reads the token from the auth store, `withCredentials: false`, `withAutomaticReconnect` with a custom backoff and a manual restart once retries are exhausted. A token change or logout restarts / stops the connection. Components and stores subscribe through `useRealtimeEvent(name, handler)` (auto-unsubscribe on scope dispose); the connection state (`connected` / `reconnecting` / `disconnected`) is exposed by a store. |
| E2 | After every successful (re)connect the client emits a synthetic `resync` event; every subscriber (feed, inbox, badges, status, open thread) refetches through REST. |
| E3 | Polling stays only as a fallback: `usePolling` and the chat / status / feed pollers run with their current intervals when the connection is not `connected`, and are off while it is. |
| E4 | New flag `realtimeMock` (default `false`, `true` in the mock Playwright config). In mock mode the plugin uses a local emitter exposed to e2e (`window.__realtime.emit(name, payload)`, `setState(...)`); the default state is `disconnected`, so existing polling-based specs keep working and new specs switch to `connected` and emit events. `e2e-real` talks to the real hub. Vitest covers handlers, `resync` and polling gating with a mocked client. |
| E5 | UI reactions: `postAdded` → refresh merchant feed and badge; `postRemoved` → remove from feed, and for an open post detail a non-dismissible banner with disabled actions (FR-FEED-5/6); `postStatusChanged` → refresh status and responses; `messageReceived` → refetch messages in the open thread (marking as read as today), otherwise unread + inbox; `threadUpdated` → refresh inbox; `notificationRaised` → toast + badge, no toast when the user is already in that thread / on that post. |
| E6 | Hub URL is `${apiBase}/hubs/app`; nginx routes `/hubs` (with `Upgrade`) in deployment; in dev the UI talks to the API directly. |

### Area F — Ticket split and sizing

| # | Decision |
|---|---|
| F1 | Three Service tickets (foundation M, post feed & status events L, chat & notification events M) and four UI tickets (client M, buyer status & responses S, merchant feed & banner M, chat & in-app notifications M). The "live panel / feed / chat" UI task from [planning.md](../docs/planning.md) is split per screen group. |
| F2 | Order: S1 → (S2, S3 in parallel). UI starts on mocks (U1) once the S1 contract is fixed; U2/U3 follow the S2 contract, U4 the S3 contract. |
| F3 | Tickets were created directly after the split was agreed (the architect waived the per-ticket review). |

---

## Assumptions & open questions

1. **Phase 7 handoff:** [FR-NOTIF-2](../docs/requirements.md) says Web Push is used only when the client has no active SignalR connection. The in-app channel in this phase does not report whether a recipient was connected; Phase 7 has to add that (e.g. a presence check) when it wires the delivery hierarchy.
2. `notificationRaised` for a merchant response is raised for positive states only (the buyer's responses list shows positive responses only, see Phase 5 C7).
3. The sender of a chat message does not receive `messageReceived`; the REST response of the send call already carries the message.
4. No active disconnecting of banned accounts and no re-authentication of a long-lived connection (B2); revisit with moderation in Phase 8.

---

## Realtime contract (shared between Service and UI tickets)

Hub: `${apiBase}/hubs/app` (SignalR). Any authenticated role; the JWT is passed as the `access_token` query string. The hub has no client-callable methods; every event is server → client, addressed per user, camelCase, and contains identifiers only. On any event (and after every reconnect) the client fetches the current state through the existing REST endpoints.

| Event | Payload | Recipient | Raised when |
|---|---|---|---|
| `postAdded` | `{ postId }` | merchant (every user account of the merchant) | the merchant has just been notified about a post (new post, or scan after onboarding / new subscription) |
| `postRemoved` | `{ postId }` | every merchant notified about the post | the post moves to `Closed`, `Fulfilled` or `Expired` |
| `postStatusChanged` | `{ postId }` | the post owner | a merchant response is created or changed; notification dispatch completes; the post ends; the post is made long-lived |
| `messageReceived` | `{ threadId, messageId }` | the other participant of the thread | a message is stored |
| `threadUpdated` | `{ threadId }` | both participants | the thread is created or its locked state changes |
| `notificationRaised` | `{ kind, postId \| null, threadId \| null }` | buyer or merchant | `kind = merchantResponded` (buyer, a merchant response moved to a positive state), `kind = newMessage` (the other participant of the thread) |

---

## Created — `[Service]`: Realtime Foundation

**Size:** M

**Brief Description**
Clients can open one authenticated real-time connection to the API, and modules have a defined way to push events to users through it.

**User Story**
As a user, I want my app to keep one live connection to the service so that updates reach me without refreshing the page.

**Description**
Brings up the Realtime module: a single hub endpoint at `/hubs/app` that accepts any authenticated user. The JWT is passed as the `access_token` query string (browsers cannot send headers on a WebSocket) and is validated exactly like on the REST API, including the account status check, when the connection is established. Each connection joins a group for its user, so that events are addressed per user; the hub exposes no client-callable methods and a client can never join anyone else's group. The module offers the other modules a way to push a named event with a payload to a user. Modules define their own channel interfaces without any SignalR dependency (the interfaces and their wiring arrive in the next two tickets); `Gdzie.Kupic.Realtime` stays the only module that references SignalR, and the channel registrations are listed explicitly in the `Program.cs` manifest. One hub and one connection serve all channels. The event names and payload records from the shared contract below are added to `API.Contract`. CORS must allow the configured frontend origins for the connection handshake without credentials. [architecture.md §6](../docs/architecture.md) and [design-decisions.md](../docs/design-decisions.md) are updated to match the agreed design (one connection, per-user addressing, identifier-only events, explicit channel registration).

**Documentation:** [architecture.md §6](../docs/architecture.md) · [requirements.md § FR-FEED, FR-NOTIF, NFR-SCALE](../docs/requirements.md) · [design-decisions.md](../docs/design-decisions.md) · [planning.md](../docs/planning.md)
**Requirements:** FR-FEED-5, FR-NOTIF-2, NFR-SCALE-1

**Definition of Done**
- [ ] A client with a valid JWT can connect to `/hubs/app` over WebSocket with the token in `access_token`; a missing, invalid or expired token and a banned account are refused
- [ ] A message pushed to a user reaches every open connection of that user and no other user (integration test with a real SignalR client)
- [ ] The hub has no client-callable methods; there is no way for a client to join or address another user's group
- [ ] The event names and payload records of the Realtime contract exist in `API.Contract`
- [ ] Realtime is the only project referencing SignalR; channel registrations are an explicit list in `Program.cs`
- [ ] The connection handshake works from the configured frontend origins without `AllowCredentials`; documented for dev and for the nginx `/hubs` route (WebSocket upgrade)
- [ ] [architecture.md §6](../docs/architecture.md) and [design-decisions.md](../docs/design-decisions.md) describe the agreed design; [planning.md](../docs/planning.md) status updated
- [ ] Unit and integration tests pass

---

## Created — `[Service]`: Post Feed & Status Events

**Size:** L

**Brief Description**
Merchants' feeds and buyers' status panels are told in real time when something changes.

**User Story**
As a merchant, I want my feed to update the moment a matching request appears or ends, and as a buyer I want my status panel to update when merchants respond, so that nobody has to refresh.

**Description**
The Marketplace module defines its own channel for these events (no SignalR dependency) and Realtime implements it with the events from the shared contract below. `postAdded` goes to every user account of each merchant that has just been notified about a post, both for a new post and for the scan that runs after onboarding or a new subscription, and only for notifications that were newly recorded. `postRemoved` goes to every merchant notified about the post when it moves to `Closed`, `Fulfilled` (buyer action) or `Expired` (the periodic expiry job, which therefore has to know which posts it has just expired). `postStatusChanged` goes to the post owner when a merchant response is created or changed, when the notification dispatch completes, when the post ends and when it is made long-lived. Events are sent only after the change is committed, and a failure to push never changes the result of the request or job; a lost event is healed by the client refetch. Web Push and email stay in Phase 7.

**Documentation:** [architecture.md §6](../docs/architecture.md) · [requirements.md § FR-FEED, FR-MATCH, FR-RESP](../docs/requirements.md) · [design-decisions.md § Real-Time Status Panel](../docs/design-decisions.md)
**Requirements:** FR-FEED-5, FR-MATCH-7, FR-POST-7, FR-POST-8

**Definition of Done**
- [ ] `postAdded` reaches exactly the newly notified merchants (all their user accounts) for a new post and for the onboarding / new-subscription scan; a repeated job run raises no duplicates
- [ ] `postRemoved` reaches every notified merchant, and no other, when a post is closed, fulfilled or expired; re-running the expiry job raises nothing for posts already expired
- [ ] `postStatusChanged` reaches only the post owner on each trigger listed in the contract
- [ ] Nothing is sent when the change is rejected or rolled back (for example `409 post_not_active`)
- [ ] A failure while pushing is logged and does not change the API response or the job result
- [ ] Marketplace has no SignalR reference; the channel is implemented in Realtime
- [ ] Unit tests with a recording test double cover every trigger; an integration test with a real SignalR client receives each event end to end

---

## Created — `[Service]`: Chat & Notification Events

**Size:** M

**Brief Description**
Chat participants receive live message and thread events, and buyers and merchants receive in-app notifications.

**User Story**
As a chat participant, I want new messages and thread changes to appear as they happen so that the conversation feels immediate.

**Description**
The Chat and Notifications modules each define their own channel (no SignalR dependency) and Realtime implements them with the events from the shared contract below. `messageReceived` goes to the other participant of the thread after a message is stored (not to the sender). `threadUpdated` goes to both participants when the thread is created by the first positive merchant response and when its locked state changes. `notificationRaised` covers the in-app part of FR-NOTIF-3: `merchantResponded` to the buyer when a merchant response moves to a positive state (this replaces the `TODO(P6)` left in Phase 5) and `newMessage` to the other participant of the thread. Events are sent only after the change is committed, and a failure to push never changes the result of the request. Delivery through Web Push or email, and the choice between the channels, stay in Phase 7.

**Documentation:** [architecture.md §6](../docs/architecture.md) · [requirements.md § FR-CHAT, FR-NOTIF](../docs/requirements.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-CHAT-5, FR-CHAT-7, FR-NOTIF-3

**Definition of Done**
- [ ] `messageReceived` reaches the other participant of the thread and not the sender; no event for a rejected message (locked thread, validation error)
- [ ] `threadUpdated` reaches both participants when the thread is created and when it is locked or unlocked
- [ ] `notificationRaised` with `merchantResponded` reaches the buyer when a merchant response moves to a positive state, not for `CantHelp`; `newMessage` reaches the other participant
- [ ] The `TODO(P6)` in the merchant response flow is replaced by the real notification
- [ ] A failure while pushing is logged and does not change the API response
- [ ] Chat and Notifications have no SignalR reference; the channels are implemented in Realtime
- [ ] Unit tests with a recording test double cover every trigger; an integration test with a real SignalR client receives each event end to end

---

## Created — `[UI]`: Realtime Client & Connection

**Size:** M

**Brief Description**
The app keeps one authenticated live connection to the service and gives screens a simple way to react to its events.

**User Story**
As a user, I want the app to stay in sync with the service in the background so that what I see is current.

**Description**
After sign-in the app opens a single connection to `/hubs/app` using the token from the auth store; it is closed on logout and restarted when the token changes (including the dev account switcher). The connection exists only in the browser (no SSR). It reconnects automatically with a backoff and keeps retrying after failures; the connection state (connected, reconnecting, disconnected) is available to the UI. Screens and stores subscribe to events by name from the [Realtime contract](#realtime-contract-shared-between-service-and-ui-tickets) and are unsubscribed automatically with their scope. After every successful (re)connect a `resync` signal makes all subscribers refetch their data through REST. The existing pollers (inbox, badges, open chat thread, post status and responses, merchant feed) keep working as a fallback: they run only while the connection is not up. A `realtimeMock` flag (off by default, on in the mock Playwright configuration, like the other mock flags) swaps the connection for a local emitter that e2e tests can drive; its default state is disconnected, so the existing polling-based specs keep working. The event contract and the connection behaviour are documented in `docs/api.md`.

**Documentation:** [architecture.md §6](../docs/architecture.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [framework.md](../gdzie-kupic-ui/docs/framework.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-FEED-5, FR-CHAT-5

**Definition of Done**
- [ ] A single connection is opened after sign-in with the stored token, is not opened for an unauthenticated user or during SSR, is closed on logout and restarted on token change
- [ ] Automatic reconnect with backoff; the connection state is observable from components
- [ ] Components and stores can subscribe to events by name and are unsubscribed when their scope ends
- [ ] A `resync` signal is emitted after every successful (re)connect and every subscriber refetches
- [ ] Existing pollers are off while the connection is up and resume when it drops
- [ ] `realtimeMock` replaces the connection with a local emitter; an e2e helper can set the state and emit events; the mock Playwright configuration enables it and existing mock specs still pass
- [ ] [api.md](../gdzie-kupic-ui/docs/api.md) documents the events, the connection and the mock helper
- [ ] Unit tests cover subscription, unsubscription, `resync`, poller gating and the token lifecycle; an e2e spec drives the mock emitter; a real-API e2e smoke test connects to the hub

---

## Created — `[UI]`: Live Buyer Status & Responses

**Size:** S

**Brief Description**
The buyer's status panel and responses list update by themselves when merchants respond.

**User Story**
As a buyer, I want to see merchants' answers appear immediately so that I can start chatting without refreshing.

**Description**
The status panel on the home screen and on the request detail page, and the list of merchant responses, refetch their data when `postStatusChanged` arrives for the post they show, and after `resync`. The counts, the matching-pending state and the responses always come from the server (REST), never from the event. When the connection is down the existing polling continues as before.

**Documentation:** [requirements.md § FR-RESP, FR-POST](../docs/requirements.md) · [design-decisions.md § Real-Time Status Panel](../docs/design-decisions.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-RESP-1, FR-POST-7, FR-POST-8

**Definition of Done**
- [ ] The status panel and the responses list refresh on `postStatusChanged` for their post and ignore events for other posts
- [ ] The "looking for merchants" state ends and counts update without a reload once the dispatch completes or merchants respond
- [ ] A post that ends (closed, fulfilled, expired) is reflected without a reload
- [ ] `resync` refetches status and responses
- [ ] With the connection down the polling fallback still works
- [ ] Unit tests and e2e tests with the mock emitter cover the event, `resync` and the fallback

---

## Created — `[UI]`: Live Merchant Feed & Closed-Post Banner

**Size:** M

**Brief Description**
The merchant feed gains and loses requests in real time, and an open request shows a banner when it ends.

**User Story**
As a merchant, I want my feed to reflect new and ended requests immediately so that I only spend time on requests that are still open.

**Description**
On `postAdded` the feed and the tab and navigation counters refresh, keeping the active tab and filters. On `postRemoved` the request disappears from the feed and the counters. If the merchant has the detail of that request open, a non-dismissible banner appears and all action buttons are disabled (the link to an existing chat thread stays available, because threads outlive the post). Feed contents and counts always come from the server (REST), never from the event. `resync` refetches the feed and the counters; with the connection down the existing polling continues.

**Documentation:** [requirements.md § FR-FEED, FR-RESP](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-FEED-5, FR-FEED-6, FR-RESP-4, FR-RESP-5

**Definition of Done**
- [ ] `postAdded` refreshes the feed and counters while keeping the active tab and filters
- [ ] `postRemoved` removes the request from the feed and updates the counters
- [ ] An open detail of a removed request shows a non-dismissible banner and disables all action buttons; the chat link stays
- [ ] A response attempt that races with the closing shows an error notification (FR-RESP-5)
- [ ] `resync` refetches the feed and counters; the polling fallback still works with the connection down
- [ ] Unit tests and e2e tests with the mock emitter cover add, removal, the banner and `resync`

---

## Created — `[UI]`: Live Chat & In-App Notifications

**Size:** M

**Brief Description**
Messages, the chat inbox, unread badges and in-app notifications update in real time.

**User Story**
As a chat participant, I want new messages and notifications to show up immediately so that I can reply without delay.

**Description**
On `messageReceived` the open thread fetches the new messages and marks them as read as it does today; for any other thread the inbox and the unread badge refresh. `threadUpdated` refreshes the inbox, including a thread's locked state. `notificationRaised` shows a toast and refreshes the badges, except when the user is already looking at that thread or that post. Message contents, unread counts and thread state always come from the server (REST), never from the event. `resync` refetches the inbox, the badges and the open thread; with the connection down the existing polling continues.

**Documentation:** [requirements.md § FR-CHAT, FR-NOTIF](../docs/requirements.md) · [design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [api.md](../gdzie-kupic-ui/docs/api.md) · [testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-CHAT-5, FR-CHAT-7, FR-NOTIF-3

**Definition of Done**
- [ ] A message received in the open thread appears without a reload and is marked as read
- [ ] A message for another thread updates the inbox and the unread badge
- [ ] `threadUpdated` refreshes the inbox, including the locked state
- [ ] `notificationRaised` shows a toast (Polish and English texts) and updates the badges; no toast when the user is already in that thread or on that post
- [ ] `resync` refetches inbox, badges and the open thread; the polling fallback still works with the connection down
- [ ] Unit tests and e2e tests with the mock emitter cover messages, inbox, toasts, suppression and `resync`

---

## Follow-ups for later phases

1. Phase 7: wire Web Push / email behind the in-app channel following the FR-NOTIF-2 hierarchy (needs knowledge of whether the recipient has an active connection).
2. Phase 8: disconnect banned accounts from the hub and re-check long-lived connections.
3. Buyer-home activity feed has no endpoint yet (empty with mocks off).
