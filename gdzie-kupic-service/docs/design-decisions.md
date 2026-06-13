# Design Decisions

A record of system design decisions made during the architecture phase.
Serves as the basis for the formal FR/NFR technical specification.

---

## 1. Geolocation & Radius Matching

**Buyer locations:**
- Buyer must add at least one saved location during account creation — required before posting
- Buyer can manage any number of saved locations at any time (e.g. "Home", "Office")
- Location input: "Find me" uses browser Geolocation API — coordinates arrive from the frontend directly, no geocoding needed
- Manual address entry: address string is geocoded via `gdzie-kupic-location-service` once and stored as coordinates with a display name
- Post creation: buyer selects from their saved locations — pure DB write, no external calls at post creation time
- Buyer sets a search radius on their post — only the buyer defines how far to search

**Merchant location:**
- Merchant location is stored in `MerchantBranches` — each branch holds `Coordinates` (`GEOGRAPHY(Point, 4326)`) along with contact details and a display name
- A merchant is matched when any of their branch locations falls within the buyer's radius + 3 km tolerance buffer
- For MVP: one branch per `Merchant` — enforced at application level; the schema supports multiple branches without changes

**Geocoding:**
- Geocoding (address → coordinates) happens once when a location is saved — never re-computed on queries or post creation
- Coordinates are stored as `GEOGRAPHY(Point, 4326)` with a spatial index for efficient radius matching

---

## 2. Real-Time Notifications to Merchants

- Post creation returns immediately to the buyer; merchant notification dispatch is asynchronous
- Matching criteria: merchant's location within post radius + buffer AND merchant subscribed to post's category/tag
- Each matched merchant is notified via Web Push (PWA) or email fallback if no active push subscription exists
- A merchant is never notified twice for the same post
- Notification event is recorded per merchant: who was notified, when, and via which channel

---

## 3. Real-Time Status Panel for Buyers

- Buyer sees live counts update as merchants respond: notified / checking / has it / can't help
- Counts are always derived from persisted state - not from in-memory counters
- On page reload or reconnect, the buyer receives current counts immediately from the server
- Real-time updates delivered via SignalR; buyer joins a channel scoped to their post
- Status panel is visible to the post owner only - merchants do not see the counts

**Post submission loading states:**
- Immediately after submission: "Looking for merchants in your area..."
- After dispatch completes with matches: "Notified X merchants" - status panel activates
- After dispatch completes with zero matches: "No merchants found in your area" - triggers zero-match popup

**Zero-match popup:**
- Message: "No merchants carrying this product are registered in your area yet. If a new merchant joins, they will be notified about your request. Would you like to keep it active longer?"
- Buyer confirms -> post becomes long-lived (extended expiry, configurable, default 14 days)
- Buyer declines -> post remains active with standard 72-hour expiry
- If a new merchant registers later and matches the post, they are notified automatically

---

## 4. Merchant Response State Machine

**States:**
- `Can't Help` - archived for the merchant (shown blurred); merchant can re-respond any time while the post is open
- `May Have It` - opens a chat thread with the buyer
- `Have It` - opens a chat thread with the buyer
- `Can Order It` - opens a chat thread with the buyer (merchant can source the item but does not have it in stock)

**Rules:**
- Any state transition to `May Have It`, `Have It`, or `Can Order It` opens a chat thread between that merchant and the buyer
- Chat threads never close on state change - once open, always open
- Merchant can change state any time while the post is active; locked once post is closed or fulfilled

**Post urgency:**
- Buyer sets an urgency flag on the post; if urgent, they also specify a deadline date
- Urgency is displayed to merchants only on urgent posts - non-urgent posts show no urgency indicator
- Informational only for MVP - no automatic filtering of responses based on urgency

---

## 5. Chat (1:1 Buyer <-> Merchant)

- A chat thread opens when a merchant transitions to `May Have It`, `Have It`, or `Can Order It`
- One thread per merchant per post - reopening the same post shows the existing thread
- Messages are persisted; history is available on reconnect or page reload
- Real-time delivery via SignalR (same connection as the status panel)
- Thread stays open after post is closed or fulfilled - buyer and merchant may still need to coordinate
- Image attachments supported in chat; maximum size is configurable (default 5 MB)
- Unread message counts shown in merchant and buyer inboxes
- No content moderation for MVP

---

## 6. Authentication & Authorization

- Three account types: `Buyer`, `MerchantAccount`, `Admin` - each with distinct permissions; no guest/anonymous access
- All content is private - posts are visible only to their owner and matched merchants; no public browsing
- JWT-based authentication: access token (lifetime configurable, default 7 days) + refresh token
- Refresh tokens are rotated on every use - old tokens are invalidated immediately
- Banned accounts are blocked immediately: account status is checked on every authenticated request; a banned user's access token is rejected regardless of its expiry
- Password reset via email: one-time link, set new password
- Buyer and Merchant self-register with separate account types; same registration flow, different role
- Admin accounts are provisioned directly - no self-registration path
- Merchant registration is open and free; no verification required for MVP

---

## 7. Post Lifecycle Management

- Post states: `Active` -> `Fulfilled / Closed / Expired`
- `Fulfilled` - buyer found what they needed through the platform (positive outcome)
- `Closed` - buyer cancelled, gave up, or found it elsewhere (neutral outcome)
- `Expired` - post reached its expiry time without being manually closed
- Buyer can transition to `Fulfilled` or `Closed` at any time while the post is active
- Posts auto-expire after 72 hours by default (configurable)
- Long-lived posts (opted in via zero-match popup) expire after 14 days by default (configurable)
- Urgent posts auto-expire at their specified deadline date instead of the default duration
- A newly registered merchant whose location and subscriptions match an active post is notified automatically - including long-lived posts with zero prior matches
- When a post is closed, expired, or fulfilled, all merchant state changes are locked
- In-flight notification dispatch is not cancelled when a post closes - merchants who receive a notification for a closed post will see it as inactive
- Chat threads remain open after post closure
- Post-MVP consideration: cancel in-flight notifications when a post is manually closed

---

## 8. Category System

- Two-level taxonomy: `Category` (bucket) -> `Tag` (product type)
- Every tag belongs to exactly one category - tags are scoped per category, not global
- Buyer selects a category first, then a tag - both are required on post creation
- Tags represent product type only (e.g. `Microphone`), never brand or model (e.g. `Shure SM7B`)
- Taxonomy is admin-curated - no user-generated categories or tags in MVP

**Merchant subscription matching:**
- Merchant subscribes to a whole category (catch-all) or to specific tags within a category, or both
- Category-level subscription matches all posts in that category regardless of tag
- Tag-level subscription matches only posts with that exact tag
- A post always carries exactly one category and exactly one tag

**Tag taxonomy seed list:**
- Defined in a separate document (to be created before launch) and linked here
- Seed list is a pre-launch deliverable, not a technical blocker

**Disabled tags:**
- Tags are soft-disabled by admin - existing subscriptions remain but the tag receives no new post matches
- Disabled tags cannot be selected on new posts
- Soft disable is reversible; no data is deleted

**Zero matching merchants:**
- Post is created and saved normally regardless of match count
- Buyer is shown immediate feedback: "No merchants in your area are subscribed to this category yet"
- Buyer sees "0 merchants notified" on the status panel - no silent failure

---

## 9. Merchant Feed

- Merchant sees a feed of active posts matching their subscribed categories/tags and location
- Feed shows only posts the merchant has not yet responded to
- Responded posts are shown separately as a flat list with the merchant's response state visible
- Default ordering: urgent posts first, then newest first within each group
- Main feed loads via infinite scroll; no pagination
- When a post is closed, fulfilled, or expired it is removed from the merchant feed in real-time via SignalR
- If the merchant has the post detail open when it closes, a non-dismissible banner is shown and all action buttons are disabled
- If a merchant submits a response to a post that closed in the race window, the server rejects it and the client shows an error notification

---

## 10. Data Persistence

- Database: PostgreSQL with PostGIS extension for spatial queries
- Background job state (Hangfire) persisted to the same PostgreSQL instance
- Chat image attachments stored in an S3-compatible object store; MinIO runs as a Docker container in the local dev environment, AWS S3 (or equivalent) in production — application code uses the same S3 API in both environments

---

## 11. PWA & Push Notifications

- Each device (merchant or buyer) registers a push subscription endpoint stored on the server
- Notification delivery follows a fallback hierarchy:
  1. SignalR — delivered in-app if the client has an active connection
  2. Web Push — sent to the device if SignalR is not connected
  3. Email — additional fallback; opt-in per user in account settings, disabled by default
- Push notifications fire for: new post matching merchant subscriptions, new chat message, merchant response received by buyer
- Expired or invalid push subscriptions are removed from the server when delivery fails
- iOS requires the PWA to be added to the home screen for push notifications to work (iOS 16.4+)

---

## 12. Admin

- Admin role is distinct from all other roles; no self-registration path
- Admin account is created by a startup seeder: reads `AdminSeed:Email` and `AdminSeed:Password` from configuration, creates the account if no admin exists yet; idempotent
- Local dev credentials are defined in `appsettings.Local.json` (gitignored); production credentials are injected via environment variables
- Admin manages the category and tag taxonomy (create, rename, soft-disable) — see section 8 for tag disable behaviour
- Admin can ban any user account (buyer or merchant); banned accounts lose all platform access immediately

**Ban effects — Buyer banned:**
- All active posts are immediately expired
- All chat threads are locked; no new messages from either side
- Merchants see a notice in each affected chat: "This user has left the application. Would you like to remove this chat?"

**Ban effects — Merchant banned:**
- Merchant cannot submit new responses to any post
- Existing responses remain visible to buyers
- All chat threads with that merchant are locked
- Buyers see a notice in each affected chat: "This merchant has left the application. Would you like to remove this chat?"
- Merchant's push subscriptions are removed from the server

---

## 13. Scalability & Fan-Out

- A single post can match many merchants simultaneously — notification dispatch must be asynchronous and non-blocking via Hangfire
- Merchants must not receive duplicate notifications; a `PostNotification` deduplication record is written before each dispatch
- On post creation, a real-time feed update is pushed via SignalR to all connected merchants in range — this is the same fan-out surface as push notifications and must be non-blocking
- Account status (banned/active) is checked on every authenticated request; this lookup should be short-lived cached (e.g. 1 minute) to avoid excessive DB load under concurrent traffic
- The merchant location match query runs on every post creation; a spatial index on `MerchantBranches.Coordinates` is required for acceptable performance at scale
- MVP targets a single application instance — no horizontal scaling, no distributed SignalR backplane, no multi-node Hangfire coordination