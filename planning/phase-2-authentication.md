# Phase 2 — Authentication — Planning Draft

> **Planning workspace, kept in the repo.** Drafts the epic + sub-issues for this phase with the architect before/alongside GitHub issue creation. See [phase-planning skill](../.github/skills/phase-planning/SKILL.md) for the workflow this follows.

Status legend: `Open` (not discussed yet) · `Discussing` · `Decided` · `Drafted` (full ticket body written below, pending review/edits) · `Ready` (approved, ready to become a ticket)

**ADR:** the backend authentication approach (incl. OAuth) is formalized as [docs/adr/0001-backend-authentication-oauth.md](../docs/adr/0001-backend-authentication-oauth.md) — full context, decision drivers, options considered, and consequences live there, not duplicated here.

---

## Epic — [Epic] Phase 2: Authentication

**Deliverable:** Register and log in as Buyer, Merchant, or Admin; JWT + refresh token flow working end-to-end (per [planning.md](../docs/planning.md)).

**Sub-issues:** _(filled in once drafted/approved)_

- [ ] #TBD ...

---

## Draft Sub-Issues — `gdzie-kupic-service` (Service)

| # | Task (from planning.md) | Size | FR-* | Status |
|---|---|---|---|---|
| 1 | User model + registration (Buyer + Merchant) + login + JWT issuance + refresh token rotation | L | FR-AUTH-1, FR-AUTH-2, FR-AUTH-3, FR-AUTH-3a, FR-AUTH-7, NFR-SEC-1, NFR-SEC-2 | Ready — see [below](#drafted-service-user-registration-login--jwtrefresh-token-issuance) |
| 2 | User mock for testing: hardcoded long-lived access token (merged with task 4) | M | — | Ready — see [below](#drafted-service-mock-login-tokens-for-test-buyer-merchant--admin-accounts) |
| 3 | Account status enforcement per-request (~1 min cache); banned rejection; refresh token theft detection | M | FR-AUTH-4, FR-AUTH-3a, NFR-SEC-3 | Ready — see [below](#drafted-service-account-status-enforcement--refresh-token-theft-detection) |
| 4 | ~~Administrator mock~~ — merged into task 2 | — | — | Merged into #2 |
| 5 | CORS: service accepts requests only from the frontend domain (already implemented Phase 1 — this ticket verifies/hardens it) | M | — | Ready — see [below](#drafted-service-cors-configuration-verification--multi-origin-support) |
| 6 | Location controller requires authentication | M | FR-AUTH-6, NFR-SEC-5 | Ready — see [below](#drafted-service-location-controller-requires-authentication) |
| 7 | Google OAuth login/registration (callback handling, account auto-link by email, role from `state` param, `ExternalLogins` table) | M | FR-AUTH-8, FR-AUTH-9 | Ready — see [below](#drafted-service-google-oauth-login--registration) |

_(Task 1 and Task 7 kept as two separate draft tickets — closely related but distinct scope. Revisit merging them if it makes review easier.)_

---

## Draft Sub-Issues — `gdzie-kupic-ui` (UI)

| # | Task (from planning.md) | Size | Status |
|---|---|---|---|
| 1 | Redirect to sign-in page when the user is not authenticated | M | Ready — see [below](#drafted-ui-redirect-to-sign-in-verification) |
| 2 | Display buttons to log in as Admin or User on the test instance (merged with task 3) | M | Ready — see [below](#drafted-ui-devtest-account-switcher) |
| 3 | ~~Display buttons to switch account or log out~~ — merged into task 2 | — | Merged into #2 |
| 4 | Access token stored and attached to every outgoing request | M | Ready — see [below](#drafted-ui-access-token-attached-to-every-request) |
| 5 | Google OAuth login/register buttons (separate for Buyer and Merchant entry points) | M | Ready — see [below](#drafted-ui-google-oauth-buttons--role-toggle-on-registration) |

---

## Notes / Running Decisions Log

- **ADR-1 decided:** custom user store + standalone Google OAuth handler (not ASP.NET Identity, not external IdP). See [ADR 0001](../docs/adr/0001-backend-authentication-oauth.md) for full rationale. `design-decisions.md`, `data-model.md`, `requirements.md`, `architecture.md` updated accordingly.
- **Service task 1 decisions:** role travels as **payload data, not routing**, for both registration and login (symmetric with the OAuth `state` param already carrying role). Registration: single endpoint, `{email, password, role}`, role restricted to `Buyer`/`Merchant` (never `Admin`). Login: single endpoint, `{email, password}`, role returned in the response — consistent with the already-existing UI `login.vue`/`register.vue`. Phase 2 merchant registration creates only the `Users` account, no `Merchants`/`MerchantAccounts`/`MerchantBranches` (that's Phase 3 onboarding); password minimum 8 characters, no further complexity rules; JWT signed with symmetric HMAC-SHA256 using a configured secret.
- **Role value:** the `Merchant` role (formerly drafted as `MerchantAccount`) was renamed to avoid confusion with the unrelated `MerchantAccounts` table (Phase 3, merchant-staff link table). All docs updated accordingly.
- **UI drafting complete:** all 5 UI sub-issues are `Ready`. UI Task #2/#3 merged into one "Dev/Test Account Switcher" ticket. The role-selection toggle needed on `register.vue` (now that registration is a single endpoint with `role` in the payload) was folded into the OAuth ticket (UI #5) rather than a separate ticket, since that ticket already touches `register.vue`/`login.vue`.

---

## Drafted — `[Service]`: User Registration, Login & JWT/Refresh Token Issuance

**Size:** L

**Brief Description**
Buyers and Merchants can self-register with email/password, log in, and receive a JWT access token + refresh token; refresh tokens rotate on every use.

**User Story**
As a buyer or merchant, I want to register and log in with my email and password so that I can securely access my account.

**Description**
Role travels as payload data, not routing, for both registration and login — symmetric with how the OAuth `state` param already carries the role. A single registration endpoint accepts `{email, password, role}` where `role` is restricted to `Buyer`/`Merchant` (self-registration as `Admin` is never allowed). A single login endpoint accepts `{email, password}` and returns the role together with the tokens, consistent with the already-existing UI `login.vue`/`register.vue`. Merchant registration in this phase creates only the `Users` account — `Merchants`/`MerchantAccounts`/`MerchantBranches` onboarding is out of scope (Phase 3). JWT signed with HMAC-SHA256 using a configured secret. Refresh tokens are stored hashed and rotated on every use.

**Documentation:** [data-model.md § Auth](../docs/data-model.md#auth) · [requirements.md](../docs/requirements.md)
**Requirements:** FR-AUTH-1, FR-AUTH-2, FR-AUTH-3, FR-AUTH-7, NFR-SEC-1, NFR-SEC-2

**Definition of Done**
- [ ] A single registration endpoint creates a `Users` row (`Status=Active`) given a valid email + password (min. 8 characters) and a `role` of `Buyer` or `Merchant`; any other role value (e.g. `Admin`) is rejected; duplicate email returns a conflict error
- [ ] A single login endpoint validates credentials (email + password) against the stored bcrypt hash for any role and rejects invalid credentials with a generic error (no user enumeration)
- [ ] Successful login returns a JWT access token (default 7-day lifetime, configurable) and a refresh token; the refresh token is stored hashed, never in plaintext
- [ ] JWT is signed with HMAC-SHA256 using a secret from configuration and contains at minimum the user id and role as claims
- [ ] JWT bearer authentication middleware is wired into the API pipeline so any endpoint can require a valid access token via standard authorization attributes
- [ ] A valid refresh token can be exchanged for a new access + refresh token pair; the previous refresh token is invalidated immediately
- [ ] An already-invalidated (rotated or expired) refresh token is rejected
- [ ] Passwords are never logged or returned in any response
- [ ] Unit tests cover password hashing, JWT issuance, and refresh rotation

---

## Drafted — `[Service]`: Google OAuth Login & Registration

**Size:** M

**Brief Description**
Buyers and MerchantAccounts can register/log in via Google OAuth in addition to password, with automatic account linking by email.

**User Story**
As a buyer or merchant, I want to sign up or log in with my Google account so that I don't need to create a separate password.

**Description**
Implements [ADR 0001](../docs/adr/0001-backend-authentication-oauth.md): standalone Google OAuth handler (not ASP.NET Identity). Role is selected before redirect and carried through the OAuth `state` parameter. On callback, an existing account with a matching email is linked automatically; otherwise a new account is created with the pre-selected role. Admin cannot be created/linked via OAuth.

**Documentation:** [adr/0001-backend-authentication-oauth.md](../docs/adr/0001-backend-authentication-oauth.md) · [design-decisions.md §6](../docs/design-decisions.md#6-authentication--authorization) · [data-model.md](../docs/data-model.md#auth)
**Requirements:** FR-AUTH-8, FR-AUTH-9

**Definition of Done**
- [ ] Initiating Google login/registration requires specifying the intended role (Buyer or Merchant) before redirecting to Google
- [ ] On callback, if no account exists with the returned email, a new `Users` row is created with the pre-selected role and linked via a new `ExternalLogins` row
- [ ] On callback, if an account already exists with the returned email, the Google identity is linked to it automatically, with no extra confirmation step
- [ ] A user with a linked Google identity logs in via Google repeatedly and always resolves to the same account
- [ ] Successful Google login/registration returns the same JWT access token + refresh token pair shape as password login
- [ ] Attempting to register/link a Google account for the Admin role is rejected
- [ ] A Google account cannot link to more than one local account, and a local account cannot link to more than one Google account (unique constraint enforced)

---

## Drafted — `[Service]`: Account Status Enforcement & Refresh Token Theft Detection

**Size:** M

**Brief Description**
Every authenticated request rejects banned accounts regardless of token validity, and reuse of an already-rotated refresh token is treated as a theft signal that revokes all of a user's refresh tokens.

**User Story**
As a platform operator, I want banned accounts to lose access promptly and stolen refresh tokens to be neutralized so that compromised or banned accounts cannot continue using the platform.

**Description**
Account status (`Active`/`Banned`) is checked on every authenticated request; the result is cached in-process (~60s absolute expiration) to avoid a DB lookup per request, so enforcement is near-immediate rather than instantaneous (bounded by the cache TTL). Refresh token rotation: presenting a refresh token whose stored record is already revoked (previously rotated) is a theft signal — all currently valid refresh tokens for that user are revoked immediately and the user must log in again. Applies uniformly to Buyer, Merchant, and Admin.

**Documentation:** [design-decisions.md §6 & §13](../docs/design-decisions.md) · [architecture.md §5](../docs/architecture.md#5-authentication--authorization)
**Requirements:** FR-AUTH-3a, FR-AUTH-4, NFR-SEC-3

**Definition of Done**
- [ ] A request with a valid JWT for a banned account is rejected regardless of the token's remaining expiry
- [ ] Account status lookups are cached in-process for ~60 seconds (absolute expiration)
- [ ] The refresh endpoint also rejects requests for a banned account
- [ ] Presenting an already-rotated (revoked) refresh token triggers revocation of all currently valid refresh tokens for that user
- [ ] After a theft-triggered revocation, any previously issued refresh token for that user fails, forcing a fresh login
- [ ] Enforcement applies uniformly to Buyer, Merchant, and Admin roles
- [ ] Unit tests cover banned-account rejection, cache expiry behaviour, and the theft-detection revocation cascade

---

## Drafted — `[Service]`: Mock Login Tokens for Test Buyer, Merchant & Admin Accounts

**Size:** M

**Brief Description**
Seeded test accounts for Buyer, Merchant, and Admin roles, each with a pre-generated, practically non-expiring JWT access token committed to documentation, so the API can be exercised without going through registration/login.

**User Story**
As a developer or reviewer, I want ready-made Buyer/Merchant/Admin tokens documented so that I can call protected endpoints immediately without registering or logging in.

**Description**
Extends the existing Admin startup seeder pattern (`FR-ADMIN-5`) to also idempotently seed a fixed test Buyer account and a fixed test Merchant account (`Users` rows only, consistent with the Phase 2 scope decision that merchant accounts don't get `Merchants`/`MerchantBranches` until Phase 3 onboarding). For all three accounts (Buyer, Merchant, and the existing `AdminSeed` account), a static JWT access token is generated once with an effectively non-expiring lifetime and committed to [local-dev.md](../docs/local-dev.md) alongside each account's email/password. This is a deliberate, accepted trade-off specific to this academic project, which never runs in a production environment with real user data — the same approach would be a security defect in a production-bound project.

**Documentation:** [local-dev.md](../docs/local-dev.md) · [design-decisions.md §12](../docs/design-decisions.md)
**Requirements:** FR-ADMIN-5 _(seeding precedent — no dedicated FR-* code for this testing convenience)_

**Definition of Done**
- [ ] A fixed test Buyer account and a fixed test Merchant account are seeded at startup (idempotent — safe to run repeatedly), analogous to the existing Admin seeder
- [ ] Each mock account also has a known email + password documented, usable via the normal login endpoint (not only the static token)
- [ ] A static, pre-generated JWT access token is produced for each of the three mock accounts (Buyer, Merchant, Admin) and committed to `local-dev.md`
- [ ] Each mock token has an effectively non-expiring lifetime (a far-future expiry) while remaining a normally-validated JWT
- [ ] Each mock token authenticates its respective account and is subject to the same authorization/account-status rules as any other token
- [ ] Documentation states plainly that these accounts/tokens exist only because this project never runs in a production environment

---

## Drafted — `[Service]`: CORS Configuration Verification & Multi-Origin Support

**Size:** M

**Brief Description**
Verify and harden the existing CORS policy so the API reliably accepts requests only from allowed frontend origins, and supports multiple origins (e.g. local dev + a deployed test instance).

**User Story**
As a platform operator, I want the API to strictly enforce which origins may call it cross-origin so that only our own frontend(s) can interact with the service from a browser.

**Description**
A CORS policy already exists in `Program.cs`, restricted to a single configured origin. This ticket verifies that behaviour with automated tests and extends configuration to support more than one allowed origin, since the project runs both a local dev frontend and a deployed test-instance frontend.

**Documentation:** [local-dev.md](../docs/local-dev.md)
**Requirements:** _no dedicated FR-* code — infrastructure hardening_

**Definition of Done**
- [ ] CORS origin configuration accepts one or more origins (e.g. comma-separated) and all of them are honoured by the policy
- [ ] An automated test confirms a request from an allowed origin succeeds and a request from a non-configured origin is rejected by CORS
- [ ] Local dev and test-instance origins are documented in `local-dev.md`
- [ ] The CORS policy does not enable credentials (cookies), consistent with the Bearer-token-only auth approach

---

## Drafted — `[Service]`: Location Controller Requires Authentication

**Size:** M

**Brief Description**
The location lookup endpoint requires a valid access token like every other endpoint — no anonymous access.

**User Story**
As a platform operator, I want every endpoint, including location lookup, to require authentication so that the API has no anonymous entry points.

**Description**
Depends on the JWT bearer middleware wired in the core registration/login ticket. Adds the authorization requirement to the existing `LocationController` and confirms unauthenticated requests are rejected.

**Documentation:** [requirements.md](../docs/requirements.md)
**Requirements:** FR-AUTH-6, NFR-SEC-5

**Definition of Done**
- [ ] All `LocationController` endpoints require a valid access token
- [ ] A request without a token, or with an invalid/expired token, is rejected
- [ ] A request with a valid token for any of the three roles succeeds
- [ ] A regression test covers the anonymous-rejection case

---

## Drafted — `[UI]`: Redirect to Sign-In (Verification)

**Size:** M

**Brief Description**
Unauthenticated users are redirected to the sign-in page when visiting a page that requires authentication.

**User Story**
As a visitor without an active session, I want to be redirected to the login page when I try to access a page that requires authentication, so that I understand I need to sign in first.

**Description**
`app/middleware/auth.ts` already implements this (redirects to `/auth/login` when `!authStore.isAuthenticated`) and is already applied to one page (`pages/requests/new.vue`) via `definePageMeta({ middleware: 'auth' })`. This ticket verifies the behaviour with tests rather than building it from scratch, consistent with the per-page opt-in pattern already documented in `FRAMEWORK.md`.

**Documentation:** [FRAMEWORK.md](../gdzie-kupic-ui/FRAMEWORK.md) · [docs/framework.md](../gdzie-kupic-ui/docs/framework.md)
**Requirements:** _no dedicated FR-* code — UI behaviour_

**Definition of Done**
- [ ] Visiting a page with `definePageMeta({ middleware: 'auth' })` while unauthenticated redirects to `/auth/login`
- [ ] Visiting the same page while authenticated does not redirect
- [ ] A test (unit or e2e) covers both cases

---

## Drafted — `[UI]`: Dev/Test Account Switcher

**Size:** M

**Brief Description**
One-click buttons to log in as the seeded test Buyer, Merchant, or Admin account, switch between them at any time, and log out.

**User Story**
As a developer or reviewer, I want to instantly switch between test Buyer/Merchant/Admin sessions without registering or typing credentials, so that I can exercise role-specific UI quickly.

**Description**
Uses the static, non-expiring mock tokens from the corresponding Service ticket (Buyer/Merchant/Admin, documented in `local-dev.md`). Clicking a button calls `authStore.setAuth(mockToken, mockUser)` directly with no network round trip, since the token is pre-generated and already valid. Switching is available at any time, even while already logged in as a different test account, with no explicit logout required first. The existing logout button in `TheHeader.vue` already clears the session. Not gated behind an environment flag, consistent with the accepted-risk decision that this project never runs in production.

**Documentation:** [local-dev.md](../docs/local-dev.md)
**Requirements:** _no dedicated FR-* code — developer/testing convenience_

**Definition of Done**
- [ ] Buttons are visible to log in as the test Buyer, Merchant, and Admin accounts, using the static tokens/user data from `local-dev.md`
- [ ] Clicking a button immediately sets the session (`authStore.setAuth`) with no API call, and the UI reflects it (email/role shown in the header)
- [ ] Switching between test accounts works at any time without requiring logout first
- [ ] The existing logout button clears the session and returns to the unauthenticated state (verify only — already built)
- [ ] Mock token/user data is read from configuration (env-driven), not hardcoded inline in the component

---

## Drafted — `[UI]`: Access Token Attached to Every Request

**Size:** M

**Brief Description**
Every outgoing request to the backend API automatically includes the access token, and an expired/invalid session is detected and cleared.

**User Story**
As a logged-in user, I want my requests to the API to be authenticated automatically so that I don't get spurious authorization failures on protected endpoints.

**Description**
`useApi()` currently does not attach an `Authorization` header at all. Since the project also uses `useFetch()` directly (with its own `baseURL`) for SSR page data, attaching the token only inside `useApi()` would miss that pattern. A shared mechanism (e.g. a Nuxt plugin providing a common `$fetch` instance with an `onRequest` hook reading the token from `authStore`) is needed so both patterns are covered without duplicating the logic at each call site. A 401 response clears the local auth state to avoid looping on a stale/invalid token.

**Documentation:** [docs/api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** _no dedicated FR-* code — UI infrastructure_

**Definition of Done**
- [ ] Every request made through `useApi()` attaches `Authorization: Bearer <token>` when the user is authenticated
- [ ] Every request made through `useFetch()` also attaches the header, via a shared mechanism (not duplicated per call site)
- [ ] When the user is not authenticated, no `Authorization` header is attached and public requests keep working as before
- [ ] A 401 response from the API clears the local auth state (`authStore.clearAuth()`)
- [ ] A test covers header attachment and 401 handling

---

## Drafted — `[UI]`: Google OAuth Buttons & Role Toggle on Registration

**Size:** M

**Brief Description**
"Continue with Google" buttons on login/register, plus a role toggle (Buyer/Merchant) on the registration form now that registration is a single endpoint taking `role` in the payload.

**User Story**
As a buyer or merchant, I want to register or log in with my Google account, and clearly choose whether I'm registering as a buyer or a merchant, so that my account is created with the right role.

**Description**
`register.vue` currently has no role selection at all and calls a single `/api/auth/register` endpoint — this ticket adds a Buyer/Merchant toggle whose value is sent as the `role` field in the same payload (per the Service Task 1 decision). It also adds a "Continue with Google" button to both `login.vue` and `register.vue`; on the registration page, the button follows whichever role is currently toggled (the role must be chosen **before** redirecting to Google, per [ADR 0001](../docs/adr/0001-backend-authentication-oauth.md), since it travels through the OAuth `state` parameter). `login.vue` needs no role toggle — its Google button doesn't need to pre-select a role, mirroring the unified password-login endpoint.

**Documentation:** [adr/0001-backend-authentication-oauth.md](../docs/adr/0001-backend-authentication-oauth.md) · [docs/api.md](../gdzie-kupic-ui/docs/api.md)
**Requirements:** FR-AUTH-8, FR-AUTH-9

**Definition of Done**
- [ ] `register.vue` has a Buyer/Merchant toggle; the selected value is sent as the `role` field of the registration payload
- [ ] `login.vue` and `register.vue` each have a "Continue with Google" button that redirects to the backend's OAuth initiation endpoint
- [ ] On `register.vue`, the Google button carries the currently toggled role to the backend (so the created/linked account gets the right role)
- [ ] On `login.vue`, the Google button requires no role input
- [ ] After a successful Google redirect back to the frontend, the returned tokens/user are stored via `authStore.setAuth(...)`, same as password login
- [ ] A failed Google login/registration (e.g. denied consent, backend error) shows an error message without crashing the page
