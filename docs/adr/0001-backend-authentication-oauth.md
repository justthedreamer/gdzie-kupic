# ADR 0001: Backend Authentication Approach (Password + Google OAuth)

- **Status:** Accepted
- **Date:** 2026-09-06
- **Phase:** P2 — Authentication

## Context

Phase 2 delivers registration and login for `Buyer`, `Merchant`, and `Admin` accounts. Before this phase, [design-decisions.md](../design-decisions.md#6-authentication--authorization) and [architecture.md](../architecture.md#5-authentication--authorization) had already established:

- JWT access token (default 7 days, configurable) + refresh token stored hashed in the database
- Refresh tokens rotated on every use; previous token invalidated immediately
- Bcrypt password hashing
- Account status (`Active`/`Banned`) checked on every authenticated request, short-lived cached (~1 min)
- Three roles, no anonymous access
- `Gdzie.Kupic.Auth` module owns registration/login/JWT/refresh/password-reset/status-enforcement, with no dependencies on other modules

During phase planning, a new requirement was added: support **OAuth login** (starting with Google) in addition to email/password, since `Gdzie.Kupic.Auth` was still an empty scaffold and nothing was implemented yet.

## Decision Drivers

- Must not conflict with the already-designed `Users` / `RefreshTokens` / `PasswordResetTokens` schema in [data-model.md](../data-model.md#auth)
- Must not conflict with `NFR-SEC-1` (bcrypt password hashing)
- Must preserve the `Auth` module's independence (no dependency on other modules, per [architecture.md](../architecture.md#2-service-responsibilities))
- Keep MVP scope small — avoid unused surface area (email confirmation, lockout, 2FA, etc.)
- Avoid new infrastructure dependencies where a self-hosted, in-process solution is sufficient

## Considered Options

### A — Full ASP.NET Core Identity

`AddIdentity<TUser, TRole>()` + `IdentityDbContext`, external login providers wired via `AddGoogle()`, native login-linking (`AspNetUserLogins`).

- ✅ Mature, well-documented; least custom code for the OAuth handshake + account linking itself
- ❌ Identity's own schema (`AspNetUsers`, `AspNetRoles`, `AspNetUserLogins`, ...) conflicts with the already-designed `Users`/`RefreshTokens` tables — would require replacing the schema or running two parallel user stores
- ❌ Default password hasher is PBKDF2, not bcrypt — conflicts with `NFR-SEC-1` unless a custom `IPasswordHasher` is plugged in, negating much of the benefit
- ❌ Identity does not itself issue JWTs — a custom token-issuance layer is still required on top, regardless of this choice
- ❌ Brings a large surface area (lockout, 2FA, email confirmation, role store) that is unused for MVP
- ❌ Weakens the "`Auth` → no module dependencies" module boundary by pulling in a large opinionated framework where a thin one would do

### B — Custom user store (as already planned) + standalone OAuth handlers

Keep the planned schema and bcrypt hashing. Use only the standalone ASP.NET Core authentication handler for Google (`Microsoft.AspNetCore.Authentication.Google`) — **not** Identity — purely for the redirect/callback exchange. On callback, find-or-create the user in the existing `Users` table and issue the same JWT + refresh token pair as password login.

- ✅ Zero conflict with the schema and decisions already made
- ✅ `Auth` module keeps zero dependencies on other modules
- ✅ Full control over JWT shape, refresh rotation, and account-status caching — one code path for both login methods after authentication succeeds
- ❌ More custom code than Option A for the callback → find-or-create → link logic (bounded, well-understood scope: 3 static roles, single provider for MVP)
- Requires one new table, `ExternalLogins`, to record linked provider identities

### C — External identity provider (Auth0 / Keycloak / Supabase Auth)

Delegate all authentication (password + OAuth) to a third-party service.

- ✅ Least custom code of all options
- ❌ New infrastructure dependency (self-hosted Keycloak) or vendor lock-in and cost (Auth0/Supabase) — conflicts with the project's self-hosted modular-monolith direction
- ❌ Rejected for MVP without further evaluation; not aligned with current infrastructure decisions in [architecture.md](../architecture.md)

## Decision

**Option B.** Custom user store + standalone Google OAuth handler.

- Google is the only OAuth provider for MVP.
- Password login and Google login coexist per account: `Users.PasswordHash` is nullable — an OAuth-only account has no password.
- Role (`Buyer` or `Merchant`) for a new OAuth account is decided **before** the redirect — separate entry points/buttons per role — and carried through the OAuth `state` parameter. The user is never asked to pick a role after the callback.
- On callback, if the returned email matches an existing account, the Google login is linked to it automatically — Google is treated as a trusted email verifier, no additional confirmation step.
- `Admin` accounts cannot be created or linked via OAuth (no self-registration path for Admin, per existing decision).
- New table: `ExternalLogins (Id, UserId, Provider, ProviderKey, CreatedAt)`, unique on (`Provider`, `ProviderKey`).

## Consequences

- `data-model.md` gains the `ExternalLogins` table; `Users.PasswordHash` becomes nullable
- `design-decisions.md` §6, `requirements.md` (`FR-AUTH-8`, `FR-AUTH-9`), and `architecture.md`'s module table are updated to reflect this decision
- The registration/login ticket for phase 2 includes the Google OAuth callback + account-linking flow as part of (or alongside) the core JWT/refresh-token ticket
- UI needs a Google login/register button per role entry point (Buyer, Merchant)
- Adding a second OAuth provider later is additive: another standalone handler + more `Provider` values in `ExternalLogins` — no schema change needed
- If ASP.NET Core Identity or an external IdP becomes desirable later (e.g. need for 2FA, enterprise SSO), that would require a new ADR superseding this one
