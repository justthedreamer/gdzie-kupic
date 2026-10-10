# Phase 7 — Push Notifications & Email — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review) · `Ready` (approved) · `#<n>` (GitHub issue created)

**Deliverable:** Merchants and buyers receive notifications when the app is not open (Web Push), and opted-in users receive e-mail ([planning.md](../docs/planning.md)).

**Requirements in scope:** FR-NOTIF-1 … FR-NOTIF-5, FR-MATCH-4 (dispatch hand-over). The in-app (SignalR) part of FR-NOTIF-3 was delivered in Phase 6.

---

## Planning areas

| # | Area | Status |
|---|---|---|
| A | Delivery hierarchy and online detection | Decided |
| B | Web Push delivery mechanics | Decided |
| C | Subscription lifecycle and cleanup | Decided |
| D | E-mail: scope, opt-in, transport | Decided |
| E | UI: service worker, permission UX, settings | Decided |
| F | Ticket split and sizing | Decided |

**State found in code/docs** (verified before planning):

1. `INotificationDispatcher` is a no-op (`NoOpNotificationDispatcher`) and only models "new post for a merchant" (`DispatchAsync(postId, merchantId)`); FR-NOTIF-3 also needs chat messages and merchant responses for buyers.
2. The `PushSubscription` entity exists, but there are no endpoints, no uniqueness on `Endpoint` and no navigation from `User`.
3. `User` has no settings columns; account endpoints are `GET/PUT /api/account/profile`.
4. The UI uses `@vite-pwa/nuxt` with `generateSW` (manifest only); a custom `push` handler needs `injectManifest`.
5. The planning task "In-app notification display" for the UI is already delivered by Phase 6 (#126); Phase 7 UI only adds the settings screen and push registration.
6. A merchant response creates the chat thread without any message (`ResponseStorage`), so the buyer's "merchant responded" and "new message" are separate events.

---

## Epic - [Epic]: Phase 7: Push Notifications & Email - [#135](https://github.com/justthedreamer/gdzie-kupic/issues/135)

**Sub-issues:**

| # | Ticket | Size | Depends on | Status |
|---|---|---|---|---|
| S1 | [Service]: Push Subscriptions & VAPID | M | — | [#136](https://github.com/justthedreamer/gdzie-kupic/issues/136) |
| S2 | [Service]: Notification Dispatch & Web Push Delivery | L | S1 | [#137](https://github.com/justthedreamer/gdzie-kupic/issues/137) |
| S3 | [Service]: E-mail Settings & Buyer E-mail Notifications | M | S2 (dispatcher) | [#138](https://github.com/justthedreamer/gdzie-kupic/issues/138) |
| S4 | [Service]: Merchant Unprocessed-Requests Digest | M | S3 | [#139](https://github.com/justthedreamer/gdzie-kupic/issues/139) |
| U1 | [UI]: Service Worker & Push Registration | M | contract S1 | [#140](https://github.com/justthedreamer/gdzie-kupic/issues/140) |
| U2 | [UI]: Notification Settings & Permission Prompt | M | U1, contract S3 | [#141](https://github.com/justthedreamer/gdzie-kupic/issues/141) |

Implementation order: S1 → S2; S3 after the dispatcher shape from S2 exists; S4 after S3; U1 after the S1 contract (on mocks), U2 after S3 contract.

---

## Decisions (agreed with the architect)

### Area A — Delivery hierarchy and online detection

| # | Decision |
|---|---|
| A1 | "Is the user online" is the number of open SignalR connections per user, held in memory inside the single API instance (counter updated on hub connect / disconnect). A user with at least one open connection gets only the SignalR event; a user with none gets Web Push (and e-mail where it applies). Multi-instance deployment would need a shared presence store; out of scope (single instance in `docker-compose`). |
| A2 | The service worker additionally suppresses a notification when one of the app's windows is focused, so a short reconnect gap never produces a duplicate. |

### Area B — Web Push delivery mechanics

| # | Decision |
|---|---|
| B1 | Web Push is sent with the `Lib.Net.Http.WebPush` library (VAPID). |
| B2 | `INotificationDispatcher` is generalised to one call per notification: a kind (`NewPost`, `MerchantResponded`, `NewMessage`), the recipient user, and the post / thread it refers to. It is called by the matching jobs (`NewPost`), the merchant response flow (`MerchantResponded`) and chat (`NewMessage`). |
| B3 | Every push is delivered by its own Hangfire job (one per recipient notification) so that a slow or failing push endpoint never blocks the caller and transient failures (5xx, 429, network) are retried by Hangfire. After a real delivery `PostNotification.Channel` / `SentAt` are filled in (they stay empty for notifications not delivered). |
| B4 | VAPID keys and subject come from configuration (`Vapid__PublicKey`, `Vapid__PrivateKey`, `Vapid__Subject`). When they are missing Web Push is disabled with a warning in the log (the service still starts); the public key is served by `GET /api/push/vapid-public-key`. |
| B5 | No batching or aggregation of notifications in this phase. |

### Area C — Subscription lifecycle and cleanup

| # | Decision |
|---|---|
| C1 | `Endpoint` is unique. Registering is an upsert by endpoint: if the endpoint already belongs to another user (account switch on the same device) it is reassigned to the current user, so the previous user never gets notifications on someone else's device. |
| C2 | Removal on the client's request: `DELETE /api/push/subscription` (logout, push switched off in settings). Removing subscriptions of banned accounts is part of Phase 8. |
| C3 | `CleanPushSubscriptionsJob(endpoint)` is a one-off job enqueued by the delivery job when the push service answers `404` / `410`; it removes the subscription (FR-NOTIF-4). No periodic sweep and no per-user subscription cap. |

### Area D — E-mail: scope, opt-in, transport

| # | Decision |
|---|---|
| D1 | E-mail is opt-in per user (`false` by default), stored on the user and exposed by `GET/PUT /api/account/notification-settings` (separate from the profile endpoints). The same setting applies to buyers and merchants; the UI wording depends on the role. Admins have no notifications. |
| D2 | **Buyer** e-mail: only when the buyer opted in, is offline, and a merchant responded (`MerchantResponded`) or wrote a chat message (`NewMessage`). For `NewMessage`, an e-mail is sent only when the message starts a series of unread messages in that thread (no e-mail per message). |
| D3 | **Merchant** gets no per-event e-mail. Instead a recurring job sends one digest e-mail per opted-in, non-banned merchant user with the number of unprocessed requests in the feed (the "new" count already computed for the feed badge), only when the number is greater than zero. Default schedule `0 9 * * *` in `Europe/Warsaw`, configurable (cron + time zone), so twice a day is a configuration change. |
| D4 | E-mail is sent through an `IEmailSender` abstraction. The only implementation for now logs the message (to Seq); SendGrid is deferred until a production deployment exists (docs updated: FR-NOTIF-5, planning.md). Integration tests use a recording sender. |
| D5 | Push is sent for `NewPost` (merchant), `MerchantResponded` (buyer) and `NewMessage` (both) when the recipient is offline (FR-NOTIF-3). |

### Area E — UI: service worker, permission UX, settings

| # | Decision |
|---|---|
| E1 | The PWA moves to `injectManifest` with an own service worker (precache + `push` + `notificationclick`). The decision logic (suppress when a window is focused, kind → URL) lives in pure functions covered by Vitest. |
| E2 | The push payload is built by the server in Polish: `{ kind, postId?, threadId?, title, body }`. No chat message content in notifications (lock screen privacy). The service worker derives the click target from `kind` and the ids: `/feed/{postId}` (new post), `/requests/{postId}` (merchant responded), `/chat/{threadId}` (new message). |
| E3 | Permission is requested only on a user gesture: a switch on the settings screen, plus a dismissible one-time banner on buyer home and the merchant feed while the permission is still undecided (dismissal remembered locally). Where the browser has no push support (e.g. iOS outside an installed PWA) the banner shows an "add to home screen" hint instead. |
| E4 | A shared settings page `/settings/notifications` (push switch with states: unsupported / blocked in the browser / on / off, and the e-mail switch) is linked from the existing placeholders in the buyer ("Ustawienia") and merchant ("Powiadomienia") navigation. |
| E5 | Registration lifecycle: subscribing on enable (`PUT`), re-sending the subscription after each sign-in while the permission is granted (the endpoint may have rotated or the device changed owner), `DELETE` on sign-out and when the switch is turned off. |
| E6 | New flag `pushMock` (default `false`, `true` in the mock Playwright config) replaces the browser push API with a controllable fake, since Playwright cannot receive real pushes. |

### Area F — Ticket split and sizing

| # | Decision |
|---|---|
| F1 | Four Service tickets (subscriptions & VAPID M, dispatch & Web Push L, e-mail settings & buyer e-mail M, merchant digest M) and two UI tickets (service worker & registration M, settings & prompt M). The two planning-doc tasks become: Web Push (S1, S2, U1), e-mail fallback and opt-in (S3, S4, U2). "In-app notification display" is dropped from the phase (done in #126). |
| F2 | Tickets are drafted and reviewed one at a time in this file; nothing is created on GitHub before the consolidated draft is confirmed. |

---

## Assumptions & open questions

1. Single API instance: the in-memory presence counter (A1) is not valid for a scaled-out deployment.
2. The digest counts merchant feed requests in the "new" tab as returned for the feed badge; its precise definition follows the feed, not a new query.
3. Notification texts are Polish only (no per-user language is stored).
4. Admin ban (Phase 8) removes the banned user's push subscriptions; until then a banned account may still hold them (it receives nothing, as matching skips banned merchants and chat is locked).

---

## Contract (shared between Service and UI tickets)

**Push subscription** (any authenticated buyer or merchant):

| Method | Path | Body | Result |
|---|---|---|---|
| `GET` | `/api/push/vapid-public-key` | — | the VAPID public key; a defined error when Web Push is not configured |
| `PUT` | `/api/push/subscription` | `{ endpoint, keys: { p256dh, auth } }` | registers or re-assigns the subscription of the calling user (idempotent) |
| `DELETE` | `/api/push/subscription` | `{ endpoint }` | removes the caller's subscription for that endpoint (idempotent) |

**Push payload** (Web Push message body, JSON): `{ kind: "newPost" | "merchantResponded" | "newMessage", postId: string | null, threadId: string | null, title: string, body: string }`.

**Notification settings** (any authenticated buyer or merchant): `GET/PUT /api/account/notification-settings` with `{ emailEnabled: boolean }`.

---

## Ready — `[Service]`: Push Subscriptions & VAPID

**Size:** M

**Brief Description**
Devices of signed-in users can register for Web Push, and the service can say whether Web Push is available.

**User Story**
As a buyer or merchant, I want my device to register for push notifications so that I can be notified when the app is not open.

**Description**
Adds the Web Push registration side of the Notifications module. A signed-in buyer or merchant can register the push subscription of a device (endpoint and keys, per the shared contract below), remove it, and read the VAPID public key the browser needs to subscribe. A subscription is identified by its endpoint: registering an endpoint that already exists is an update, and when the endpoint belongs to another user (the device changed owner after a sign-out / sign-in) it is reassigned to the caller, so the previous user never receives notifications meant for someone else's device. Removing a subscription only affects the caller's own and is idempotent. The VAPID keys and subject come from configuration; when they are not set the service still starts, logs a warning, and the public-key endpoint answers with a defined error so that clients can hide push. The `User` → `PushSubscriptions` relation left as a TODO is completed and the data model docs are updated. Sending pushes is a separate ticket.

**Documentation:** [architecture.md](../docs/architecture.md) · [requirements.md § FR-NOTIF](../docs/requirements.md) · [data-model.md § PushSubscriptions](../docs/data-model.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-NOTIF-1

**Definition of Done**
- [ ] `PUT /api/push/subscription` stores the subscription for the caller; repeating the call with the same endpoint creates no duplicate and updates the keys
- [ ] Registering an endpoint that belongs to another user moves it to the caller; the previous user no longer has it
- [ ] `DELETE /api/push/subscription` removes only the caller's subscription for that endpoint and answers successfully when there is none
- [ ] Invalid input (empty or non-HTTPS endpoint, missing keys, over-long values) is rejected with a validation error
- [ ] The endpoints require authentication and are available to buyers and merchants only (an admin gets `403`)
- [ ] `GET /api/push/vapid-public-key` returns the configured public key; with no VAPID configuration it returns a defined error and the service still starts, logging a warning
- [ ] `Endpoint` is unique in the database (migration included); the `User` navigation TODO is resolved
- [ ] [data-model.md](../docs/data-model.md) and the API documentation describe the endpoints and the uniqueness rule
- [ ] Unit and integration tests pass

---

## Ready — `[Service]`: Notification Dispatch & Web Push Delivery

**Size:** L

**Brief Description**
Buyers and merchants who are not connected to the app receive a Web Push notification when a new request matches, a merchant responds, or a chat message arrives.

**User Story**
As a buyer or merchant, I want to be notified on my device when something happens while the app is closed so that I do not miss requests, responses or messages.

**Description**
Replaces the no-op dispatcher with the real notification flow of FR-NOTIF-2. Three events produce a notification: a merchant has been notified about a new post (`NewPost`, to every user account of that merchant), a merchant response has moved to a positive state (`MerchantResponded`, to the buyer; not for `CantHelp`, as in Phase 6) and a chat message has been stored (`NewMessage`, to the other participant, not the sender). The dispatcher is called by the matching jobs, the merchant response flow and chat. For each recipient the service decides by presence: a user with at least one open real-time connection already receives the in-app event and gets no push; a user with none gets a Web Push on every registered device. The notification text is built on the server in Polish and does not contain the content of a chat message; the payload follows the shared contract below. Each push is delivered by its own background job so that a slow or failing push service never blocks the caller, and transient failures are retried. When the push service reports that a subscription no longer exists (`404` / `410`) the subscription is removed by `CleanPushSubscriptionsJob`; other failures never remove it (FR-NOTIF-4). After a real delivery of a `NewPost` push, `Channel` and `SentAt` of the post notification record are filled in; they stay empty otherwise. When VAPID is not configured nothing is sent and the jobs do not fail. Calling the dispatcher never changes the outcome of the request or job that raised the event. The e-mail channel is added to the same flow by the next ticket.

**Documentation:** [architecture.md](../docs/architecture.md) · [requirements.md § FR-NOTIF, FR-MATCH](../docs/requirements.md) · [data-model.md § PostNotifications, PushSubscriptions](../docs/data-model.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-NOTIF-2, FR-NOTIF-3, FR-NOTIF-4, FR-MATCH-4

**Definition of Done**
- [ ] A user with at least one open real-time connection gets no push; the same user gets pushes on every registered device as soon as the last connection is closed (tested with several simultaneous connections)
- [ ] `NewPost` reaches every user account of each newly notified merchant, never a banned merchant or the post's author; re-running a matching job sends no duplicates
- [ ] `MerchantResponded` reaches the buyer for a positive response only; `NewMessage` reaches the other participant and not the sender; nothing is sent for a rejected message or a locked thread
- [ ] The push payload matches the shared contract, is in Polish and contains no chat message content
- [ ] Each push is delivered by its own background job; transient failures are retried and do not affect the caller
- [ ] A `404` / `410` answer from the push service removes that subscription through `CleanPushSubscriptionsJob`; other errors keep it
- [ ] `PostNotification.Channel` / `SentAt` are filled in after a delivered `NewPost` push and stay empty otherwise
- [ ] With no VAPID configuration nothing is sent, a warning is logged and no job fails
- [ ] A failure in the dispatcher is logged and never changes the API response or the job result
- [ ] [architecture.md](../docs/architecture.md) describes the delivery hierarchy as implemented; [planning.md](../docs/planning.md) status updated
- [ ] Unit tests (recording test doubles for the push client) and integration tests pass

## Ready — `[Service]`: E-mail Settings & Buyer E-mail Notifications

**Size:** M

**Brief Description**
Users can opt in to e-mail notifications, and buyers who opted in receive an e-mail when a merchant responds or writes to them while they are offline.

**User Story**
As a buyer, I want to receive an e-mail when a merchant responds or writes to me while I am away so that I can come back to my request in time.

**Description**
Adds a per-user e-mail notification setting, off by default for every account (existing ones included), that a signed-in buyer or merchant can read and change (shared contract below; an admin has no notifications). E-mail becomes the third step of the delivery hierarchy of FR-NOTIF-2 in the dispatcher from the previous ticket, independent of Web Push: it is sent only to a user who opted in, is not banned and has no open real-time connection. For buyers it covers two events: a merchant response that moved to a positive state (`MerchantResponded`) and a chat message from a merchant (`NewMessage`); for `NewMessage` an e-mail is sent only when the message starts a series of unread messages for the buyer in that thread, so a running conversation does not produce an e-mail per message. Merchants receive no e-mail for new posts or chat messages (their digest is a separate ticket). Messages are in Polish, link to the matching page of the app (the request or the conversation; the base address comes from configuration), contain no chat message content and tell the user where to switch e-mail notifications off. E-mails go through a sender abstraction whose only implementation for now writes the message to the log; a real provider is deferred (FR-NOTIF-5 in [requirements.md](../docs/requirements.md)). Each e-mail is sent by its own background job with retries, and a failure never affects the request or job that raised the event.

**Documentation:** [architecture.md](../docs/architecture.md) · [requirements.md § FR-NOTIF](../docs/requirements.md) · [data-model.md § Users](../docs/data-model.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-NOTIF-2, FR-NOTIF-3, FR-NOTIF-5

**Definition of Done**
- [ ] `GET/PUT /api/account/notification-settings` read and change the caller's e-mail setting; it is `false` for every existing and new account; an admin gets `403`
- [ ] A buyer who opted in and is offline gets an e-mail for a positive merchant response and for a merchant's chat message
- [ ] No e-mail is sent to a user who did not opt in, who has an open real-time connection, or who is banned
- [ ] For chat messages only the first message of an unread series triggers an e-mail; after the buyer reads the thread the next message triggers one again
- [ ] No e-mail is sent to a merchant for a new post or a chat message
- [ ] The message is in Polish, links to the right page, has no chat message content and says how to turn e-mail notifications off
- [ ] E-mails are sent by a background job with retries; a failure never changes the API response or the job result
- [ ] The default sender only logs the message; the sender can be replaced without changing the dispatcher; tests use a recording sender
- [ ] [data-model.md](../docs/data-model.md) (new setting) and the API documentation are updated
- [ ] Unit and integration tests pass

## Ready — `[Service]`: Merchant Unprocessed-Requests Digest

**Size:** M

**Brief Description**
Merchants who opted in receive a periodic e-mail with the number of requests in their feed that they have not processed yet.

**User Story**
As a merchant, I want a regular summary of the requests waiting in my feed so that I do not have to open the app to know whether there is something to answer.

**Description**
A recurring background job sends one digest e-mail to each merchant user account that opted in to e-mail notifications (setting from the previous ticket) and is not banned. The e-mail states how many requests the merchant has not processed yet: the same number as the "new" counter of the merchant feed (active, not expired, not yet responded to), computed per merchant. Nothing is sent when the number is zero. The digest is a summary, so it is sent regardless of whether the user is currently connected. When a merchant has several user accounts, each account that opted in receives the e-mail. The schedule is configurable (cron expression and time zone) with the default of once a day at 09:00 in `Europe/Warsaw`; twice a day is a configuration change, not a code change. The message is in Polish, links to the merchant feed, tells where to switch e-mail notifications off, and is sent through the same sender abstraction and background-job rules as the buyer e-mails (default sender only logs). A failure for one recipient does not stop the others and a re-run of the job within the same schedule slot does not send a second digest to a recipient that already received it. Merchants get no other e-mails (FR-NOTIF-5, scope agreed in Phase 7 planning).

**Documentation:** [architecture.md](../docs/architecture.md) · [requirements.md § FR-NOTIF, FR-FEED](../docs/requirements.md) · [design-decisions.md](../docs/design-decisions.md) · [planning.md](../docs/planning.md)
**Requirements:** FR-NOTIF-5

**Definition of Done**
- [ ] On schedule, every opted-in, non-banned merchant user account with at least one unprocessed request receives one digest; accounts without opt-in, banned accounts and merchants with zero unprocessed requests receive nothing
- [ ] The number in the e-mail equals the "new" counter of that merchant's feed at the time of sending
- [ ] A merchant with several user accounts: each opted-in account receives the digest
- [ ] The digest is sent regardless of the user's real-time connection
- [ ] Cron expression and time zone are configurable; the default is once a day at 09:00 `Europe/Warsaw`; changing them changes the schedule without a code change
- [ ] The message is in Polish, links to the merchant feed and says how to turn e-mail notifications off
- [ ] A failure for one recipient does not stop the others; re-running the job in the same schedule slot sends no duplicate
- [ ] The default sender only logs the message; tests use a recording sender
- [ ] [planning.md](../docs/planning.md) status and the configuration documentation are updated
- [ ] Unit and integration tests pass

## Ready — `[UI]`: Service Worker & Push Registration

**Size:** M

**Brief Description**
The app shows push notifications while it is closed and keeps the device's push subscription registered with the service for the signed-in user.

**User Story**
As a buyer or merchant, I want my device to show a notification when something happens while the app is closed, and to open the right page when I tap it.

**Description**
The PWA gets its own service worker (the manifest, installability and offline caching keep working as today). On a push message (payload per the shared contract below) it shows a notification with the server-built title and text and the app icon; it shows nothing when one of the app's windows is focused, because the in-app event already covers that case, and it ignores a malformed payload without error. Tapping the notification focuses an open app window or opens a new one, on the page that matches the notification: a new request opens the merchant feed entry, a merchant response opens the buyer's request, a new message opens the conversation. The app also gets the registration logic that the settings screen and the banner (next ticket) use: the state of push on this device (not supported, push not configured on the server, blocked in the browser, off, on), turning it on (asks for permission on a user action, subscribes with the server's VAPID public key and registers the subscription), turning it off (unsubscribes and removes the registration), re-registering the current subscription after every sign-in while the permission is granted (the endpoint may have changed or the device changed owner), and removing the registration on sign-out. A new flag `pushMock` (default off, on in the mock Playwright config) replaces the browser push API with a controllable fake because real pushes cannot be received in e2e. This ticket adds no screens; the switch and banner come in the next ticket. Real delivery needs the Service tickets and a VAPID configuration.

**Documentation:** [requirements.md § FR-NOTIF](../docs/requirements.md) · [architecture.md](../docs/architecture.md) · UI [docs/api.md](../gdzie-kupic-ui/docs/api.md) · UI [docs/testing.md](../gdzie-kupic-ui/docs/testing.md)
**Requirements:** FR-NOTIF-1, FR-NOTIF-2, FR-NOTIF-3

**Definition of Done**
- [ ] The app is still installable, and the offline caching it had before keeps working with the new service worker (verified in a production build)
- [ ] A push message shows a notification with the title and text from the payload; with a focused app window nothing is shown; a malformed payload is ignored without error
- [ ] Tapping a notification focuses an existing app window or opens one, on the feed entry (`newPost`), the request (`merchantResponded`) or the conversation (`newMessage`)
- [ ] Turning push on asks for permission only from a user action, subscribes with the server's VAPID key and registers the subscription; turning it off unsubscribes and removes the registration
- [ ] The state is correct in every case: browser without push support, push not configured on the server, permission blocked, off, on
- [ ] While the permission is granted the subscription is re-registered after every sign-in; sign-out removes the registration on the server
- [ ] `pushMock` replaces the browser push API with a fake that mock e2e tests control; with the flag off the real API is used
- [ ] [api.md](../gdzie-kupic-ui/docs/api.md) lists the push endpoints and [testing.md](../gdzie-kupic-ui/docs/testing.md) describes `pushMock`
- [ ] Unit tests cover the notification decision logic and the registration lifecycle; mock e2e tests cover registration after sign-in and removal on sign-out

## Ready — `[UI]`: Notification Settings & Permission Prompt

**Size:** M

**Brief Description**
Buyers and merchants can switch push and e-mail notifications on and off in one place, and are invited to enable push at a sensible moment.

**User Story**
As a buyer or merchant, I want to choose how the app notifies me so that I get the notifications I want and nothing more.

**Description**
A settings page for notifications, shared by buyers and merchants inside their own shell and not available to admins, reachable from the entries that are placeholders today: "Ustawienia" in the buyer navigation and "Powiadomienia" in the merchant navigation. It has two switches. The push switch reflects the state of push on this device from the previous ticket: when push is not supported or not configured on the server it is disabled with an explanation, when the browser blocks it the page explains how to unblock it, otherwise switching it on or off registers or removes the device. The e-mail switch reads and saves the account setting (off by default) and its wording depends on the role: buyers are told they get an e-mail about merchant responses and new messages while away, merchants that they get a regular summary of unprocessed requests. Loading and saving errors are shown and a failed save leaves the switch in its previous state. While push has not been decided yet (permission still undecided) a dismissible banner invites buyers on the home page and merchants on the feed to turn it on; dismissing it is remembered on this device, and it is not shown when push is already on, blocked, or not configured. Where the browser cannot do push (for example iOS outside an installed app) the banner offers an "add to home screen" hint instead. Texts are available in Polish and English, and the screen is usable on mobile and desktop and with the keyboard.

**Documentation:** [requirements.md § FR-NOTIF](../docs/requirements.md) · [specification.md](../docs/specification.md) · UI [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · UI [docs/api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-NOTIF-1, FR-NOTIF-5

**Definition of Done**
- [ ] The page opens from the buyer "Ustawienia" and merchant "Powiadomienia" navigation entries in the role's own shell, and an admin cannot open it
- [ ] The push switch shows the correct state and explanation for: not supported, not configured on the server, blocked in the browser, off, on; switching it registers or removes this device
- [ ] The e-mail switch loads and saves the account setting; the wording differs for buyers and merchants; a failed load or save shows an error and keeps the previous state
- [ ] The banner appears on buyer home and on the merchant feed only while the permission is undecided and push is available; dismissing it hides it on this device for good; it never appears when push is on, blocked or not configured
- [ ] Where push is not supported the banner shows the "add to home screen" hint instead of the switch
- [ ] Polish and English texts exist for every new string; the screen works on mobile and desktop and with the keyboard
- [ ] The notification settings endpoints are added to the mock API and to [api.md](../gdzie-kupic-ui/docs/api.md)
- [ ] Unit tests cover the switch states and the banner conditions; mock e2e tests cover both switches, the error state, the banner and its dismissal on mobile and desktop; the real-API e2e suite covers saving the e-mail setting
