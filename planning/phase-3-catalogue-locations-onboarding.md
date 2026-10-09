# Phase 3 — Catalogue, Locations & Merchant Onboarding — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review) · `Ready` (approved) · `#<n>` (GitHub issue created)

**Deliverable:** Admin can manage taxonomy; buyer has saved locations; merchant is fully onboarded with location + subscriptions (per [planning.md](../docs/planning.md)).

**Goal of this split:** Service and UI tickets can be implemented in parallel by two agents. The UI agent works against the **API contract** below (mocking responses until the Service tickets land); the Service agent implements exactly that contract.

---

## Epic — [Epic] Phase 3: Catalogue, Locations & Merchant Onboarding — [#44](https://github.com/justthedreamer/gdzie-kupic/issues/44)

**Sub-issues:**

| # | Ticket | Size | Depends on | Status |
|---|---|---|---|---|
| S1 | [Service]: Catalogue Provisioning (Seeded Categories & Tags) | M | — | [#45](https://github.com/justthedreamer/gdzie-kupic/issues/45) |
| S2 | [Service]: Category & Tag Management (Admin) + Read Endpoints + Cache | M | S1 | [#46](https://github.com/justthedreamer/gdzie-kupic/issues/46) |
| S3 | [Service]: Buyer Saved Locations | M | — | [#47](https://github.com/justthedreamer/gdzie-kupic/issues/47) |
| S4 | [Service]: Merchant Onboarding | L | — | [#48](https://github.com/justthedreamer/gdzie-kupic/issues/48) |
| S5 | [Service]: Merchant Category/Tag Subscriptions | M | S1, S4 | [#49](https://github.com/justthedreamer/gdzie-kupic/issues/49) |
| U1 | [UI]: Category & Tag Management (Admin) | M | contract S2 | [#50](https://github.com/justthedreamer/gdzie-kupic/issues/50) |
| U2 | [UI]: Buyer Saved Locations | M | contract S3 | [#51](https://github.com/justthedreamer/gdzie-kupic/issues/51) |
| U3 | [UI]: Merchant Onboarding Flow | L | contract S1/S2 (read), S4, S5 | [#52](https://github.com/justthedreamer/gdzie-kupic/issues/52) |
| U4 | [UI]: Buyer Home Page | L | — (mocked data; real wiring in Phase 4/5) | [#59](https://github.com/justthedreamer/gdzie-kupic/issues/59) |
| U5 | [UI]: Merchant Home Page (Requests Feed) | L | — (mocked data; real wiring in Phase 4/5) | [#62](https://github.com/justthedreamer/gdzie-kupic/issues/62) |

U1–U3 and S1–S5 come 1:1 from [planning.md](../docs/planning.md) Phase 3.1 / 3.2. U4 was added mid-phase at the architect's request (buyer default page after login, based on the [buyer mockup](../gdzie-kupic-ui/docs/ui/buyer.png)). U5 is its Merchant counterpart (based on the [merchant mockup](../gdzie-kupic-ui/docs/ui/merchant.png)) and generalises the Buyer shell into shared components.

---

## Assumptions & open questions (confirm before creating issues)

1. **Seeding mechanism (S1):** idempotent startup seeder with stable IDs that inserts only missing rows (same pattern as the existing admin/mock-account seeding) — so admin renames/disables are never overwritten on restart. _Alternatives: EF `HasData` migration; upsert-by-ID seeder (overwrites admin edits)._
2. **Category soft-disable:** [FR-CAT-1](../docs/requirements.md) says admin can soft-disable **categories and tags**, but [data-model.md](../docs/data-model.md#catalogue) only has `IsDisabled` on `Tags`. Assumed: follow the requirement and add the flag to categories, updating `data-model.md`. _Alternative: tags only, amend FR-CAT-1._
3. **Subscribing to a disabled category/tag:** existing subscriptions stay ([FR-CAT-3](../docs/requirements.md)); assumed that **new** subscriptions to a disabled category/tag are rejected.
4. **Saved-location owner:** `data-model.md` says `UserId`; the current domain class has `BuyerId` + TODO. Assumed: owner is the buyer's user account (`UserId`); no separate buyer profile in MVP.
5. **Forward geocoding is missing:** the `Location` module only does reverse geocoding (coords → address). Manual-address entry ([FR-LOC-3](../docs/requirements.md)) needs address → coordinates, so it is **part of S3/S4** (not a separate ticket). Note: the Google API key in the local stack is currently expired/missing — manual-address flows cannot be verified end to end until it is renewed.
6. **Merchant onboarding state:** a Merchant-role user who has not onboarded has only a `Users` row (Phase 2 decision). One merchant per user (`MerchantAccounts.UserId` is unique). Onboarding creates `Merchant` + `MerchantBranch` + `MerchantAccount` in one transaction with a **single** branch (multiple branches are post-MVP).
7. **Out of scope here:** `NotifyNewMerchantJob` (Phase 4), post creation (Phase 4), Google OAuth (deferred to the end; Phase 2 epic untouched).
8. **Proposed seed list** — see the last section; needs your edit/approval (design-decisions §8 treats the list as a pre-launch deliverable).

---

## API contract (shared between Service and UI tickets)

All endpoints require a valid access token. Errors use the existing `ProblemDetails` format. Roles are enforced server-side (`403` for wrong role, `401` for no/invalid token).

### Catalogue

| Method + route | Role | Request | Response |
|---|---|---|---|
| `GET /api/catalogue/categories` | any authenticated | — | `200` list of `{ id, name, isDisabled, tags: [{ id, name, isDisabled }] }`, ordered by name; includes disabled items (clients filter) |
| `POST /api/admin/categories` | Admin | `{ name }` | `201` category · `409` duplicate name |
| `PUT /api/admin/categories/{categoryId}` | Admin | `{ name }` | `200` category · `404` · `409` |
| `POST /api/admin/categories/{categoryId}/disable` · `/enable` | Admin | — | `204` · `404` |
| `POST /api/admin/categories/{categoryId}/tags` | Admin | `{ name }` | `201` tag · `404` · `409` duplicate within category |
| `PUT /api/admin/tags/{tagId}` | Admin | `{ name }` | `200` tag · `404` · `409` |
| `POST /api/admin/tags/{tagId}/disable` · `/enable` | Admin | — | `204` · `404` |

### Buyer saved locations

| Method + route | Role | Request | Response |
|---|---|---|---|
| `GET /api/saved-locations` | Buyer | — | `200` list of `{ id, displayName, latitude, longitude, createdAt }` (caller's own only) |
| `POST /api/saved-locations` | Buyer | `{ displayName, latitude, longitude }` **or** `{ displayName, address }` (exactly one location method) | `201` saved location · `400` validation / address not found · `502`-style problem when geocoding fails |
| `DELETE /api/saved-locations/{id}` | Buyer | — | `204` · `404` (also when it belongs to another user) |

### Merchant onboarding & subscriptions

| Method + route | Role | Request | Response |
|---|---|---|---|
| `GET /api/merchant/me` | Merchant | — | `200` `{ merchantId, name, description, branch: { id, displayName, latitude, longitude, addressDisplayName, phone, website } }` · `404` when not onboarded yet |
| `POST /api/merchant/onboarding` | Merchant | `{ name, description?, branch: { displayName, phone?, website?, latitude, longitude **or** address } }` | `201` same shape as `GET /api/merchant/me` · `409` already onboarded · `400` validation |
| `GET /api/merchant/subscriptions` | Merchant (onboarded) | — | `200` list of `{ id, categoryId, tagId \| null }` |
| `POST /api/merchant/subscriptions` | Merchant (onboarded) | `{ categoryId, tagId? }` (`tagId` omitted = whole category) | `201` subscription · `400` tag not in category / disabled target · `409` duplicate · `403`/`404` when not onboarded |
| `DELETE /api/merchant/subscriptions/{id}` | Merchant (onboarded) | — | `204` · `404` |

---

## Drafted — `[Service]`: Catalogue Provisioning (Seeded Categories & Tags)

**Size:** M

**Brief Description**
A predefined set of categories and tags is always present in the database, created automatically at startup with stable identifiers.

**User Story**
As a platform operator, I want the product taxonomy provisioned automatically so that buyers and merchants always have categories and tags to work with.

**Description**
Persistence for categories and tags (schema + migration) plus a startup provisioning step that creates the agreed seed list. Provisioning is idempotent and never overwrites changes an admin has made later (renames, disabled flags). Identifiers of seeded rows are stable across environments so other data (e.g. subscriptions, posts) can reference them reliably. The agreed seed list is documented and linked from the design decisions. Also adds the category disable flag if the open question #2 is confirmed.

**Documentation:** [data-model.md § Catalogue](../docs/data-model.md#catalogue) · [design-decisions.md §8](../docs/design-decisions.md#8-category-system) · [architecture.md](../docs/architecture.md)
**Requirements:** FR-CAT-2

**Definition of Done**
- [ ] `Categories` and `Tags` tables exist via a migration, with unique category names, tag names unique within a category, and `Tags.CategoryId` FK
- [ ] On startup the agreed seed categories and tags are created if missing; running startup repeatedly creates no duplicates
- [ ] Seeded categories and tags keep the same IDs on every fresh database
- [ ] Re-running startup does not revert an admin rename or a disabled flag on a seeded row
- [ ] The seed list is documented and linked from `design-decisions.md` §8
- [ ] Integration test verifies the seeded data is present after startup and provisioning is idempotent

---

## Drafted — `[Service]`: Category & Tag Management (Admin) + Read Endpoints + Cache

**Size:** M

**Brief Description**
Admins can create, rename and soft-disable categories and tags; all authenticated users can read the taxonomy, served from a cache that is invalidated on admin writes.

**User Story**
As an admin, I want to curate the category and tag taxonomy so that buyers and merchants always work with a relevant, consistent catalogue.

**Description**
Implements the endpoints defined in the [API contract](#api-contract-shared-between-service-and-ui-tickets) for the catalogue. Soft-disabling never deletes data; existing merchant subscriptions to a disabled tag remain, while a disabled tag cannot be selected on new posts. The read path is cached in memory and invalidated on every admin write, with a 5 minute fallback TTL. Depends on the catalogue provisioning ticket (S1).

**Documentation:** [requirements.md](../docs/requirements.md) · [data-model.md § Catalogue](../docs/data-model.md#catalogue) · [design-decisions.md §8](../docs/design-decisions.md#8-category-system) · [architecture.md](../docs/architecture.md)
**Requirements:** FR-CAT-1, FR-CAT-2, FR-CAT-3, FR-ADMIN-1, NFR-PERF-3a

**Definition of Done**
- [ ] Endpoints from the API contract exist for create/rename/disable/enable of categories and tags, restricted to the Admin role (Buyer/Merchant get 403, anonymous 401)
- [ ] Duplicate category names and duplicate tag names within the same category are rejected with 409; the same tag name in different categories is allowed
- [ ] Disabling a tag/category does not delete or modify any other data and is reversible via enable
- [ ] `GET /api/catalogue/categories` returns every category with its tags (including `isDisabled`) to any authenticated role
- [ ] The read result is cached and an admin write is immediately reflected in the next read; cache expires after 5 minutes at the latest
- [ ] Unit/integration tests cover role restrictions, uniqueness rules, soft-disable/enable, and cache invalidation

---

## Drafted — `[Service]`: Buyer Saved Locations

**Size:** M

**Brief Description**
A buyer can save named locations using browser coordinates or a typed address, list them, and delete them.

**User Story**
As a buyer, I want to save named locations like "Home" or "Office" so that I can reuse them when creating posts.

**Description**
Implements the saved-location endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). A location is saved either from coordinates sent by the frontend (no geocoding) or from a manual address, which is geocoded once on save via the Google Geocoding API in the `Location` module (address → coordinates). Geocoding happens only at save time. A buyer has any number of saved locations and only ever sees their own. Includes the persistence (schema + migration) with a spatial column and index.

**Documentation:** [data-model.md § Location](../docs/data-model.md#location) · [architecture.md](../docs/architecture.md) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-LOC-2, FR-LOC-3, FR-LOC-4, FR-LOC-5, NFR-PERF-2

**Definition of Done**
- [ ] A buyer can create a saved location by sending coordinates; no external geocoding call is made
- [ ] A buyer can create a saved location by sending an address; it is geocoded once and the resulting coordinates are stored
- [ ] A request containing both or neither of coordinates/address, a blank name, or out-of-range coordinates is rejected with a validation error
- [ ] An address that cannot be geocoded, or a geocoding provider failure, returns a descriptive error and stores nothing
- [ ] The list endpoint returns only the caller's own locations; deleting another user's location returns 404
- [ ] Coordinates are stored as `GEOGRAPHY(Point, 4326)` with a GIST index; the schema is created by a migration
- [ ] Endpoints are restricted to the Buyer role
- [ ] Tests cover both input methods, validation, ownership isolation, and geocoding failure handling

---

## Drafted — `[Service]`: Merchant Onboarding

**Size:** L

**Brief Description**
A merchant user completes onboarding by creating their business and a branch with a location, all in one atomic step.

**User Story**
As a merchant, I want to register my business and its location so that I can be matched with buyers nearby.

**Description**
Implements the onboarding endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). A single request creates the `Merchants` row, the `MerchantAccounts` link to the calling user, and one `MerchantBranches` row in one transaction — either all are created or none. Branch location uses the same two input methods as buyers (coordinates, or an address geocoded once on save; the forward-geocoding capability is shared with the saved-locations ticket). A user can onboard once. A merchant who has not onboarded yet is identifiable via the "me" endpoint. Includes the schema + migration for the merchant tables with a spatial index on branch coordinates. Subscriptions are handled in a separate ticket.

**Documentation:** [data-model.md § Marketplace](../docs/data-model.md#marketplace) · [architecture.md](../docs/architecture.md) · [requirements.md](../docs/requirements.md) · [design-decisions.md](../docs/design-decisions.md)
**Requirements:** FR-LOC-3, FR-LOC-5, FR-LOC-6, NFR-PERF-1

**Definition of Done**
- [ ] A Merchant user can onboard with business name, optional description and one branch (display name, optional phone/website, and a location by coordinates or address)
- [ ] `Merchants`, `MerchantAccounts` and `MerchantBranches` rows are created in a single transaction; a failure (e.g. geocoding error) leaves no partial data
- [ ] A user who already onboarded gets a 409; Buyer and Admin roles are rejected with 403
- [ ] `GET /api/merchant/me` returns the merchant + branch for an onboarded merchant and 404 for a not-yet-onboarded one
- [ ] Branch coordinates are stored as `GEOGRAPHY(Point, 4326)` with a GIST index (plus `MerchantId` index); schema created by a migration
- [ ] The mock Merchant test account is unaffected and can onboard through the endpoint
- [ ] Tests cover atomicity, duplicate onboarding, role restrictions, and both location input methods

---

## Drafted — `[Service]`: Merchant Category/Tag Subscriptions

**Size:** M

**Brief Description**
An onboarded merchant can subscribe to whole categories or specific tags, list their subscriptions, and remove them.

**User Story**
As a merchant, I want to choose which categories and tags I'm interested in so that I'm only matched with relevant buyer posts.

**Description**
Implements the subscription endpoints from the [API contract](#api-contract-shared-between-service-and-ui-tickets). A subscription is either category-level (no tag — catch-all) or tag-level; a merchant may hold both. Existing subscriptions to a tag that is later disabled remain; creating a new subscription to a disabled category/tag is rejected. Includes persistence (schema + migration). Depends on S1 (catalogue) and S4 (merchant).

**Documentation:** [data-model.md § MerchantSubscriptions](../docs/data-model.md#marketplace) · [design-decisions.md §8](../docs/design-decisions.md#8-category-system) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-CAT-3, FR-CAT-5, FR-MATCH-3

**Definition of Done**
- [ ] An onboarded merchant can add a category-level subscription and tag-level subscriptions, and both can coexist for the same category
- [ ] A tag that does not belong to the given category, or an unknown category/tag, is rejected with a validation error
- [ ] Subscribing to a disabled category or tag is rejected; already existing subscriptions to a since-disabled tag stay listed
- [ ] A duplicate `(merchant, category, tag)` subscription is rejected with 409 (unique constraint enforced in the database)
- [ ] List returns only the caller's subscriptions; deleting another merchant's subscription returns 404
- [ ] Non-onboarded merchants and non-Merchant roles cannot use the endpoints
- [ ] Tests cover category-level vs tag-level, uniqueness, disabled-target rules, and ownership isolation

---

## Drafted — `[UI]`: Category & Tag Management (Admin)

**Size:** M

**Brief Description**
An admin page to list the taxonomy and create, rename, and disable/enable categories and tags.

**User Story**
As an admin, I want to manage categories and tags in the UI so that I can keep the catalogue up to date without touching the database.

**Description**
Admin-only page consuming the catalogue endpoints of the [API contract](#api-contract-shared-between-service-and-ui-tickets). Shows all categories with their tags, including disabled ones with a clear visual state. Follows the existing design system, i18n and API-layer conventions (domain composable under `composables/api/`, auth middleware, role gating). Can be developed against mocked responses until the Service tickets land.

**Documentation:** [docs/api.md](../gdzie-kupic-ui/docs/api.md) · [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [FRAMEWORK.md](../gdzie-kupic-ui/FRAMEWORK.md) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-CAT-1, FR-ADMIN-1

**Definition of Done**
- [ ] An Admin sees a page listing every category with its tags; disabled categories/tags are visibly distinguished
- [ ] The Admin can create a category, create a tag inside a category, rename either, and disable/enable either
- [ ] Duplicate-name and other API errors are shown as readable messages without breaking the page
- [ ] The page is reachable only by authenticated Admins (unauthenticated users go to sign-in; Buyer/Merchant are denied)
- [ ] UI text is localised via the existing i18n setup and styled with the design-system tokens
- [ ] Unit/e2e tests cover list rendering and at least create + disable flows against mocked API responses

---

## Drafted — `[UI]`: Buyer Saved Locations

**Size:** M

**Brief Description**
Buyers can add saved locations (browser geolocation or typed address), see the list, and delete entries.

**User Story**
As a buyer, I want to manage my saved locations so that I can quickly reuse them when I create a post.

**Description**
Buyer-only page consuming the saved-location endpoints of the [API contract](#api-contract-shared-between-service-and-ui-tickets). Two ways to add: a "Find me" action that reads coordinates via the browser Geolocation API and sends them directly, and manual address entry that sends the address for server-side geocoding. Handles denied geolocation permission and geocoding failures gracefully. Follows existing design-system, i18n and API-layer conventions. Can be developed against mocked responses until the Service tickets land.

**Documentation:** [docs/api.md](../gdzie-kupic-ui/docs/api.md) · [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-LOC-2, FR-LOC-3

**Definition of Done**
- [ ] A Buyer sees a list of their saved locations (name + coordinates or place label) and an empty state when there are none
- [ ] A Buyer can add a location with a name using "Find me" (browser geolocation) or by typing an address
- [ ] Denied/unavailable geolocation and address-not-found / API errors are shown as readable messages
- [ ] A Buyer can delete a saved location with a confirmation step and the list updates
- [ ] The page is reachable only by authenticated Buyers
- [ ] UI text is localised and styled with the design-system tokens
- [ ] Tests cover list, add (both methods), delete, and the error states against mocked API responses

---

## Drafted — `[UI]`: Merchant Onboarding Flow

**Size:** L

**Brief Description**
A guided flow for a new merchant to enter business + branch details, set the branch location, and choose category/tag subscriptions.

**User Story**
As a merchant, I want a guided setup of my business, location and interests so that I can start receiving relevant buyer posts.

**Description**
Multi-step flow consuming the onboarding, catalogue-read and subscription endpoints of the [API contract](#api-contract-shared-between-service-and-ui-tickets): (1) business name/description and branch details, with the branch location set via "Find me" or typed address (same two methods as buyers); (2) subscription selection — whole category or specific tags, taken from the catalogue (disabled items not selectable). A Merchant who has not onboarded yet is directed into the flow; an onboarded merchant is not. Follows existing design-system, i18n and API-layer conventions. Can be developed against mocked responses until the Service tickets land.

**Documentation:** [docs/api.md](../gdzie-kupic-ui/docs/api.md) · [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [requirements.md](../docs/requirements.md) · [design-decisions.md §8](../docs/design-decisions.md#8-category-system)
**Requirements:** FR-LOC-3, FR-LOC-6, FR-CAT-5

**Definition of Done**
- [ ] A Merchant without onboarding is directed to the onboarding flow; an onboarded Merchant is not forced into it
- [ ] The branch step supports both location methods ("Find me" and typed address) and optional phone/website
- [ ] The subscription step lets the merchant pick whole categories and/or specific tags from the catalogue; disabled items are not selectable
- [ ] Submitting creates the merchant via the onboarding endpoint and then the chosen subscriptions; failures show a readable message and allow retrying without re-entering data
- [ ] An onboarded merchant can later view and remove their subscriptions
- [ ] The flow is reachable only by authenticated Merchants
- [ ] UI text is localised and styled with the design-system tokens
- [ ] Tests cover the happy path and key error states against mocked API responses

---

## Ready — `[UI]`: Buyer Home Page

**Size:** L

**Brief Description**
After signing in, a Buyer lands on a Home page that summarises their active requests, live dispatch status, recent merchant activity and recent chats, inside a responsive Buyer app shell (sidebar on desktop, bottom tab bar on mobile).

**User Story**
As a buyer, I want a home page that shows the state of my requests and conversations at a glance so that I immediately know whether merchants have responded.

**Description**
Implements the Buyer part of the [buyer mockup](../gdzie-kupic-ui/docs/ui/buyer.png) (top row: desktop Dashboard and mobile "My Request" screen). The Home page (`/home`) is the Buyer's default page: successful login/registration as a Buyer and a visit to `/` while signed in as a Buyer both land on it. Anonymous visitors, Merchants and Admins keep their current behaviour. Request images from the mockup are out of scope for now.

Posts, merchant responses and chats do not exist in the backend until Phases 4–5, so this ticket builds the **layout, widgets and navigation** against mocked data behind a single data-access seam that is replaced by real endpoints when Phases 4–5 land. With no data (the real state until then) every widget renders a designed empty state. No new backend endpoints are defined here.

### Buyer app shell (layout)

New layout used by all authenticated Buyer pages (Home and the existing Saved Locations page).

| Viewport | Structure |
|---|---|
| Desktop (`lg` and up) | Left sidebar: logo, role label ("Buyer"), navigation, "Install App" card, user card (avatar, name, role) at the bottom. Main area: page header + content. |
| Mobile (below `lg`) | Top bar (back arrow, page title, overflow `...` menu) and fixed bottom tab bar: **Dashboard · Requests · (+) New Request · Chats · Profile**. The centre (+) is a prominent round action button. |

Sidebar navigation (desktop):

| Item | Destination | State in this ticket |
|---|---|---|
| Dashboard | `/home` | active (this ticket) |
| My Requests | `/requests` | link to the existing page; full list built in Phase 4 |
| Chats | `/chats` | disabled ("soon") until Phase 5 |
| Saved Searches | — | disabled ("soon"); separate future feature |
| Saved Locations | `/saved-locations` | active (ticket #51); not in the mockup, added so the existing page stays reachable |
| Profile | — | disabled ("soon"); no ticket planned |
| Settings | — | disabled ("soon"); no ticket planned |

"Install App" card ("Get instant notifications") is rendered but inert until the PWA work in Phase 7.

### Dashboard page — widgets

Desktop layout, top to bottom (mobile equivalents below):

1. **Page header** — title "Dashboard", greeting "Welcome back, {first name}!" and a primary **"+ New Request"** button (links to `/requests/new`).
2. **Active Requests strip** — horizontally scrollable row of compact request cards with a chevron to scroll further. Each card: title and a one-line status — **"Live"** (green dot) while the request is still collecting responses, otherwise "{n} response(s)". Exactly one card is selected (highlighted with a green border); selecting another card updates widgets 3–4. The most recently posted active request is selected by default. Empty state: illustration + "You have no active requests" + New Request button (widgets 3–5 are then hidden).
3. **Selected Request summary** — title, short description, and a detail list: location (city, country), radius (km), budget (optional, hidden when absent), category/tag; a "Posted {n} min ago" chip and a **"View Details →"** link to `/requests/{id}` (rendered disabled until the Phase 4 request routes exist).
4. **Live Status** — card with a green "Live" badge, a large **notified-merchants count** ("Merchants notified"), and a four-row breakdown, each with a coloured dot and count:
   - amber — *Checking availability* ("I may have it"; the mockup draws it blue, but the design system reserves amber/warning for "checking")
   - green — *Have it* ("I have it")
   - red — *Can't help* ("I can't help")
   - grey — *No response yet*

   Counts always sum to the notified total. Static snapshot in this phase (live updates arrive in Phase 6).
5. **Recent Activity** — list of the latest merchant response events for the **selected request** (top 5, newest first): merchant avatar/icon, merchant name, event text ("Has offered", "Is checking availability", "Can't help"), and relative time ("2 min ago"). Icon colour follows the response state. Empty state: "No merchant activity yet". "View all" is rendered disabled until Phase 4.
6. **Recent Chats** — latest conversations: merchant avatar, merchant name, last-message preview (single line, truncated), and relative time. Clicking a row goes to `/chats/{id}` (disabled until Phase 5). Empty state: "No conversations yet".

Mobile layout (the mockup shows the request-focused screen under the **Dashboard** tab):

- Title "My Request" for the selected request; the Active Requests strip stays at the top for switching.
- Single column: request summary card (title, location + radius, posted time, Live badge) → **Live Status** card → **Latest Activity** card with a "View all" link.
- Recent Chats is not part of the mobile Dashboard; chats are reached through the **Chats** tab.

### Pages and sub-pages in the mockup

| Page | Route | Where it is delivered |
|---|---|---|
| Home / Dashboard (desktop + mobile "My Request" view) | `/home` | **This ticket** |
| Buyer app shell (sidebar / bottom tabs) | layout | **This ticket** |
| Saved Locations | `/saved-locations` | #51 (moved into the new shell here) |
| New Request (title, optional description, category, location, radius 5–50 km, budget, "Publish Request") | `/requests/new` | Phase 4 — post creation form (budget and radius slider need a requirements check then) |
| My Requests / Request detail | `/requests`, `/requests/{id}` | Phase 4 |
| Chats / Chat thread | `/chats`, `/chats/{id}` | Phase 5 |
| Profile, Settings | — | not planned |

The lower half of the mockup (Merchant feed, "Respond to this request", merchant chat) belongs to the Merchant role and Phase 5, not to this ticket.

**Documentation:** [buyer mockup](../gdzie-kupic-ui/docs/ui/buyer.png) · [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [docs/api.md](../gdzie-kupic-ui/docs/api.md) · [FRAMEWORK.md](../gdzie-kupic-ui/FRAMEWORK.md) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-POST-4, FR-POST-10 (display of state only — data wired in Phases 4–5)

**Definition of Done**
- [ ] A Buyer is redirected to `/home` after login/registration and when visiting `/` while signed in; Merchants/Admins/anonymous users are unaffected
- [ ] `/home` is reachable only by authenticated Buyers (anonymous → sign-in, other roles denied)
- [ ] Buyer app shell: sidebar with navigation, install card and user card on desktop; top bar + bottom tab bar with centre (+) action on mobile; Saved Locations page uses it
- [ ] Navigation items whose destination does not exist yet are visibly disabled, not broken links
- [ ] Dashboard renders the header, Active Requests strip, Selected Request summary, Live Status, Recent Activity and Recent Chats on desktop, and the stacked single-column variant on mobile, matching the mockup within design-system tokens
- [ ] Selecting a request in the strip updates the summary, Live Status and activity widgets; the most recent active request is selected by default
- [ ] Live Status breakdown counts sum to the notified total and use the colours from the design system status palette
- [ ] Every widget has an empty state; a Buyer with no requests sees a clear call to action to create one
- [ ] Widgets get their data from a single mocked data source, so Phases 4–5 can replace it with real endpoints without reworking the widgets
- [ ] Relative times and all UI text are localised (pl/en) via the existing i18n setup
- [ ] Unit tests cover widget rendering + empty states + request selection; e2e test covers login-as-Buyer → lands on Dashboard on desktop and mobile viewports

---

## Ready — `[UI]`: Merchant Home Page (Requests Feed)

**Size:** L

**Brief Description**
After signing in, an onboarded Merchant lands on the Requests Feed: new purchase requests from buyers in their area, with one-tap response buttons, a request details page and a responsive Merchant app shell (sidebar on desktop, bottom tab bar on mobile).

**User Story**
As a merchant, I want a feed of nearby purchase requests that I can answer with one tap so that I can quickly tell buyers whether I have what they are looking for.

**Description**
Implements the Merchant part of the [merchant mockup](../gdzie-kupic-ui/docs/ui/merchant.png) (desktop **Requests Feed**, **Request Details** and the mobile feed). The feed (`/feed`) is the Merchant's default page: successful login/registration as a Merchant and a visit to `/` while signed in as a Merchant both land on it. A Merchant who has not onboarded yet is still sent to onboarding first (existing guard from #52). Anonymous visitors, Buyers and Admins keep their current behaviour. Request images from the mockup are out of scope for now.

Posts, matching and merchant responses do not exist in the backend until Phases 4–5, so this ticket builds the **layout, widgets, navigation and local response interaction** against mocked data behind a single data-access seam (`useMerchantFeedApi`, same pattern as `useBuyerHomeApi`) that is replaced by real endpoints in Phases 4–5. With no data (the real state until then) the feed renders a designed empty state. No new backend endpoints are defined here.

### Shared app shell

The Buyer shell (#59) is generalised into role-agnostic shell components (sidebar, bottom tab bar, mobile top bar) driven by a per-role navigation config; the Buyer layout is moved onto them with no visible change, and a new `merchant` layout uses the same components.

| Viewport | Structure |
|---|---|
| Desktop (`lg` and up) | Left sidebar: logo, role label ("Merchant"), navigation, "Install App" card, user card (avatar, shop name, role) at the bottom. Main area: page header + content. |
| Mobile (below `lg`) | Top bar (back arrow on sub-pages, page title, overflow `...` menu) and fixed bottom tab bar: **Feed · Responses · Chats · Profile** (no centre action — merchants do not create requests). |

Sidebar navigation (desktop):

| Item | Destination | State in this ticket |
|---|---|---|
| Requests Feed | `/feed` | active (this ticket) |
| My Responses | — | disabled ("soon") until Phase 5 |
| Chats | — | disabled ("soon") until Phase 5 |
| Profile | — | disabled ("soon"); no ticket planned |
| Shop Settings | `/merchant/subscriptions` | active — the existing subscriptions page (#52) moves into the new shell; it is the "Shop Settings" entry of the mockup |
| Notifications | — | disabled ("soon") until Phase 7 |

On mobile the tabs are Feed / Responses / Chats / Profile; Shop Settings and Notifications live in the overflow menu. Unread/new badges from the mockup are not rendered until the data exists (Phase 5/6). The onboarding flow keeps its current standalone layout (the merchant has no shop yet).

### Requests Feed (`/feed`)

1. **Page header** — title "Requests Feed", subtitle "New purchase requests from buyers in your area." and a refresh button (re-runs the data load).
2. **Filters** — category (all categories present in the loaded data), maximum distance (5 / 10 / 15 / 25 / 50 km), sort (Newest first — urgent requests first, then newest, per FR-FEED-3 — or Nearest first). Desktop: one row under the header. Mobile: behind a filter button in the page header.
3. **Tabs** — **New** (not answered yet, with a count badge), **Responded** (answered, flat list with the response state visible, per FR-FEED-2), **All**. Default: New. Same tabs on desktop and mobile.
4. **Request cards** — title, short description, distance from the branch ("5.2 km"), budget ("up to 2 000 PLN", hidden when absent), category/tag, relative posted time, an "Urgent" badge when applicable, and a **"New"** badge until answered (afterwards a badge with the merchant's response). Clicking the card body opens the details page.
5. **Response buttons** on every card (and on the details page): **I have it** (green), **I may have it** (amber), **I can't help** (red). The mockup shows three; the fourth FR-RESP-1 state (`CanOrderIt`) is not in the mockup and is added by the Phase 5 response ticket. Clicking sets the response and can be changed at any time (FR-RESP-3). In this ticket the response is **local state** on the mocked data (kept in a store, survives navigation between feed and details, lost on reload) — the seam exposes `respond(id, state)` so Phase 5 only swaps the implementation. A "can't help" request stays in Responded, rendered dimmed (FR-RESP-1 "archived").
6. **Empty states** — no requests at all ("No requests in your area yet — we will notify you"), none in the current tab/filter ("Nothing here"), load error with retry.

Infinite scroll, real-time insert/removal and the post-closed banner (FR-FEED-4..6) are Phase 5/6 and not part of this ticket.

### Request Details (`/feed/{id}`)

Separate page on all viewports with a back arrow to the feed, as in the mockup:

- Title, "New"/response badge, description, detail list: buyer (display name), location (city + distance from the shop), budget (optional), category, posted time, urgency + deadline when urgent.
- **Live Status** card (same widget as the Buyer home: notified count + checking / have / can't help / no-response breakdown, design-system status colours).
- **Buyer Location** — approximate-area card: the buyer's search radius and city. A real map is not part of this ticket (no map component in the stack yet); the card is a static placeholder illustration with the radius and city text.
- **Your Response** — current response ("You haven't responded yet." when none) and the three response buttons.
- Unknown id → "Request not found" state with a link back to the feed.

### Pages in the mockup and where they are delivered

| Page | Route | Where it is delivered |
|---|---|---|
| Requests Feed (desktop + mobile) | `/feed` | **This ticket** |
| Request Details | `/feed/{id}` | **This ticket** (mock data, local response state) |
| Merchant app shell (sidebar / bottom tabs) | layout | **This ticket** (shared with Buyer) |
| Shop Settings | `/merchant/subscriptions` | #52 (moved into the new shell here) |
| My Responses, Chats / Chat thread | — | Phase 5 |
| Profile, Notifications | — | not planned in this phase (Notifications: Phase 7) |

**Documentation:** [merchant mockup](../gdzie-kupic-ui/docs/ui/merchant.png) · [docs/design-system.md](../gdzie-kupic-ui/docs/design-system.md) · [docs/api.md](../gdzie-kupic-ui/docs/api.md) · [FRAMEWORK.md](../gdzie-kupic-ui/FRAMEWORK.md) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-FEED-1..3, FR-RESP-1 (three of four states), FR-RESP-3 (display and local change only — wired in Phases 4–5)

**Definition of Done**
- [ ] A Merchant is redirected to `/feed` after login/registration and when visiting `/` while signed in (an un-onboarded merchant still goes to onboarding); Buyers/Admins/anonymous users are unaffected
- [ ] `/feed` and `/feed/{id}` are reachable only by authenticated Merchants (anonymous → sign-in, other roles denied)
- [ ] Shell components are shared by Buyer and Merchant: sidebar with navigation, install card and user card on desktop; top bar + bottom tabs on mobile; Buyer pages look and behave as before; the subscriptions page uses the Merchant shell
- [ ] Navigation items whose destination does not exist yet are visibly disabled, not broken links
- [ ] Feed renders header, filters, New / Responded / All tabs and request cards on desktop and mobile, matching the mockup within design-system tokens
- [ ] Category, distance and sort filters work (urgent first for "Newest first"); the New tab shows a count of unanswered requests
- [ ] The three response buttons set and change the response on cards and on the details page; the card moves from New to Responded and shows the response; the state is kept when navigating feed ↔ details
- [ ] Details page shows the request, Live Status (counts sum to the notified total), the buyer-location placeholder and "Your Response"; unknown id shows a not-found state
- [ ] Every list has an empty state; a load failure offers a retry
- [ ] Data comes from a single mocked source behind one seam, so Phases 4–5 can replace it with real endpoints without reworking the widgets
- [ ] Relative times, distances, currency and all UI text are localised (pl/en)
- [ ] Unit tests cover the feed logic (filtering, sorting, tabs, response state), widgets and empty states; e2e tests cover login-as-Merchant → lands on the feed, responding from a card, opening details, on desktop and mobile viewports; existing Buyer and Merchant e2e tests still pass

---

## Proposed seed list (needs approval — informs S1)

| Category | Tags |
|---|---|
| Elektronika | Telefon, Laptop, Telewizor, Słuchawki |
| Audio i muzyka | Mikrofon, Gitara, Keyboard, Głośnik |
| AGD i dom | Pralka, Lodówka, Odkurzacz, Ekspres do kawy |
| Narzędzia i budowa | Wiertarka, Szlifierka, Zestaw kluczy, Drabina |
| Sport i turystyka | Rower, Namiot, Plecak, Buty trekkingowe |
| Motoryzacja | Opony, Akumulator, Olej silnikowy, Wycieraczki |

---

## Notes / Running Decisions Log

- **GitHub issues created:** epic [#44](https://github.com/justthedreamer/gdzie-kupic/issues/44) + 8 sub-issues ([#45](https://github.com/justthedreamer/gdzie-kupic/issues/45)–[#52](https://github.com/justthedreamer/gdzie-kupic/issues/52)). Each ticket embeds the relevant slice of the API contract so it is self-contained for parallel work.
- Assumptions 1–6 above were accepted as-is ("lecimy"); the proposed seed list still needs the architect's edit/approval before S1 is implemented.
- **U4 (Buyer Home Page) decisions (confirmed):** route `/home`; built on mocked data because posts/responses/chats have no backend until Phases 4–5; images out of scope, budget optional; "Saved Searches" stays in the sidebar as a disabled placeholder (separate feature); Active Requests strip stays on top on mobile and Recent Chats is desktop-only.
- Phase 2 epic [#21](https://github.com/justthedreamer/gdzie-kupic/issues/21) and open tickets (#23, #31 — Google OAuth) are intentionally left untouched; OAuth is deferred to the end of the project.
