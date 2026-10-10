# Data Model

This document defines the database schema for the Gdzie Kupic platform.
All tables live in a single PostgreSQL database used by `gdzie-kupic-service`.

---

## Auth

### `Users`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Email` | `text` | Unique, not null |
| `PasswordHash` | `text` | Bcrypt; nullable - OAuth-only accounts have no password |
| `FirstName` | `varchar(50)` | Nullable; the only personal name other users see (a Merchant sees the Buyer's first name in the feed and chat, falling back to "Kupujący" when empty). Letters, spaces, hyphens and apostrophes only. Collected at sign-up (optional), prefilled from Google (`given_name`) when the account has none, editable via `PUT /api/account/profile`. No surname is stored |
| `Role` | `text` | `Buyer`, `Merchant`, `Admin` |
| `Status` | `text` | `Active`, `Banned` |
| `EmailNotificationsEnabled` | `boolean` | Not null, default `false` (existing accounts too); e-mail opt-in, read/changed via `GET/PUT /api/account/notification-settings` |
| `CreatedAt` | `timestamptz` | |
| `BannedAt` | `timestamptz` | Nullable |

### `ExternalLogins`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `Provider` | `text` | `Google` for MVP |
| `ProviderKey` | `text` | Provider's stable subject/user id |
| `CreatedAt` | `timestamptz` | |

Unique constraint on (`Provider`, `ProviderKey`) - one external identity links to exactly one account.

### `RefreshTokens`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `TokenHash` | `text` | Hashed token value |
| `ExpiresAt` | `timestamptz` | |
| `RevokedAt` | `timestamptz` | Nullable; set on rotation or logout |
| `CreatedAt` | `timestamptz` | |

### `PasswordResetTokens`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `TokenHash` | `text` | One-time use |
| `ExpiresAt` | `timestamptz` | |
| `UsedAt` | `timestamptz` | Nullable |

---

## Location

### `SavedLocations`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `DisplayName` | `text` | e.g. "Home", "Office" |
| `Coordinates` | `geography(Point, 4326)` | Spatial index |
| `CreatedAt` | `timestamptz` | |

---

## Catalogue

### `Categories`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Name` | `text` | Unique |
| `IsDisabled` | `bool` | Soft-disable; default false |
| `CreatedAt` | `timestamptz` | |

### `Tags`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `CategoryId` | `uuid` | FK → Categories |
| `Name` | `text` | Unique within category |
| `IsDisabled` | `bool` | Soft-disable; default false |
| `CreatedAt` | `timestamptz` | |

---

## Marketplace

### `Merchants`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Name` | `text` | Business name (e.g. "Media Expert") |
| `Description` | `text` | Nullable |
| `Status` | `text` | `Active`, `Banned` |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |
| `BannedAt` | `timestamptz` | Nullable |

### `MerchantAccounts`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `MerchantId` | `uuid` | FK → Merchants |
| `UserId` | `uuid` | FK → Users; unique |
| `CreatedAt` | `timestamptz` | |

### `MerchantBranches`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `MerchantId` | `uuid` | FK → Merchants |
| `DisplayName` | `text` | Branch name (e.g. "Media Expert Kraków Galeria Krakowska") |
| `Coordinates` | `geography(Point, 4326)` | Spatial index |
| `Phone` | `text` | Nullable |
| `Website` | `text` | Nullable |
| `AddressDisplayName` | `text` | Nullable; human-readable address shown to buyers |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

### `Posts`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `BuyerId` | `uuid` | FK → Users |
| `Coordinates` | `geography(Point, 4326)` | Copied from buyer's selected saved location at post creation time; not a FK |
| `RadiusKm` | `decimal` | Nullable; buyer-defined search radius, null = unlimited |
| `CategoryId` | `uuid` | FK → Categories |
| `TagId` | `uuid` | FK → Tags |
| `Title` | `text` | |
| `Description` | `text` | Nullable |
| `IsUrgent` | `bool` | |
| `UrgentDeadline` | `timestamptz` | Nullable; required when IsUrgent = true |
| `Status` | `text` | `Active`, `Fulfilled`, `Closed`, `Expired` |
| `NotificationDispatchStatus` | `text` | `Pending`, `Dispatched` |
| `ExpiresAt` | `timestamptz` | |
| `IsLongLived` | `bool` | Set via zero-match popup opt-in |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

### `MerchantSubscriptions`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `MerchantId` | `uuid` | FK → Merchants |
| `CategoryId` | `uuid` | FK → Categories |
| `TagId` | `uuid` | Nullable; null = category-level catch-all |
| `CreatedAt` | `timestamptz` | |

**Unique constraint**: `(MerchantId, CategoryId, TagId)`

### `MerchantResponses`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `PostId` | `uuid` | FK → Posts |
| `MerchantId` | `uuid` | FK → Merchants |
| `State` | `text` | `CantHelp`, `MayHaveIt`, `HaveIt`, `CanOrderIt` |
| `CreatedAt` | `timestamptz` | |
| `UpdatedAt` | `timestamptz` | |

**Unique constraint**: `(PostId, MerchantId)`

---

## Notifications

### `PostNotifications`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `PostId` | `uuid` | FK → Posts |
| `MerchantId` | `uuid` | FK → Merchants |
| `CreatedAt` | `timestamptz` | When the match was recorded |
| `Channel` | `text` | Nullable; `WebPush` (set after the first real push of a `NewPost` notification), `Email` is not used for merchants |
| `SentAt` | `timestamptz` | Nullable; empty until a push was actually delivered |

**Unique constraint**: `(PostId, MerchantId)` — deduplication guard

### `DigestDeliveries`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users (cascade) |
| `Slot` | `timestamptz` | UTC time of the schedule slot (latest cron occurrence) the digest belongs to |
| `SentAt` | `timestamptz` | |

**Unique constraint**: `(UserId, Slot)` - a re-run or retry within a slot sends no second merchant digest to the user.

### `PushSubscriptions`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `Endpoint` | `varchar(2048)` | Web Push endpoint URL (HTTPS) |
| `P256dhKey` | `varchar(256)` | Client public key of the subscription (`keys.p256dh`) |
| `AuthKey` | `varchar(256)` | Client auth secret of the subscription (`keys.auth`) |
| `CreatedAt` | `timestamptz` | |

**Unique constraint**: `Endpoint` - a device endpoint belongs to one user at a time. Registering an existing endpoint updates its keys and, when it belonged to another user (account switch on the same device), reassigns it to the caller. Index on `UserId`; rows are deleted with the user (cascade).

**API** (buyers and merchants; admin gets `403`):

| Method | Path | Body | Result |
|---|---|---|---|
| `GET` | `/api/push/vapid-public-key` | - | `200 { "publicKey" }`; `503` (`code = push_not_configured`) when VAPID is not configured |
| `PUT` | `/api/push/subscription` | `{ endpoint, keys: { p256dh, auth } }` | `204`, idempotent upsert by endpoint; `400` for an empty, non-HTTPS or over-long (endpoint > 2048, key > 256) value or missing keys |
| `DELETE` | `/api/push/subscription` | `{ endpoint }` | `204`, removes the caller's own subscription; idempotent |
| `GET` | `/api/account/notification-settings` | - | `200 { "emailEnabled" }` (buyer, merchant; admin `403`) |
| `PUT` | `/api/account/notification-settings` | `{ emailEnabled }` | `200 { "emailEnabled" }`; idempotent |

---

## Chat

### `ChatThreads`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `PostId` | `uuid` | FK → Posts |
| `MerchantId` | `uuid` | FK → Merchants |
| `IsLocked` | `bool` | Set when buyer or merchant is banned |
| `BuyerLastReadAt` | `timestamptz` | Nullable; messages after it are unread for the buyer |
| `MerchantLastReadAt` | `timestamptz` | Nullable; messages after it are unread for the merchant |
| `CreatedAt` | `timestamptz` | |

**Unique constraint**: `(PostId, MerchantId)` — one thread per merchant per post

### `ChatMessages`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `ThreadId` | `uuid` | FK → ChatThreads |
| `SenderId` | `uuid` | FK → Users |
| `Body` | `text` | Nullable if message is image-only |
| `AttachmentKey` | `text` | Nullable; S3 object key |
| `CreatedAt` | `timestamptz` | |

---

## Infrastructure

### `Outbox`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `Type` | `text` | e.g. `NotifyMerchants`, `NotifyNewMerchant` |
| `Payload` | `jsonb` | Job-specific data (e.g. `{ "postId": "..." }`) |
| `CreatedAt` | `timestamptz` | |
| `ProcessedAt` | `timestamptz` | Nullable; null = not yet processed (partial index on unprocessed entries) |

---

## Indexes

| Table | Index | Type | Purpose |
|---|---|---|---|
| `MerchantBranches` | `Coordinates` | GIST | Radius match query on post creation |
| `MerchantBranches` | `MerchantId` | B-tree | Branch lookup by merchant |
| `MerchantAccounts` | `UserId` | Unique B-tree | Account lookup by user identity |
| `MerchantAccounts` | `MerchantId` | B-tree | Accounts lookup by merchant |
| `SavedLocations` | `Coordinates` | GIST | Optional — future buyer proximity features |
| `Posts` | `Coordinates` | GIST | Spatial queries (if needed for future buyer proximity features) |
| `Posts` | `Status, ExpiresAt` | B-tree | Expiry job scan |
| `Posts` | `BuyerId` | B-tree | Buyer post history |
| `Outbox` | `CreatedAt` where `ProcessedAt IS NULL` | Partial B-tree | Relay scan of unprocessed entries |
| `PostNotifications` | `(PostId, MerchantId)` | Unique B-tree | Deduplication |
| `MerchantResponses` | `(PostId, MerchantId)` | Unique B-tree | One response per merchant per post |
| `ChatThreads` | `(PostId, MerchantId)` | Unique B-tree | One thread per merchant per post |
| `RefreshTokens` | `UserId` | B-tree | Token lookup on refresh |
| `MerchantSubscriptions` | `MerchantId` | B-tree | Subscription lookup during matching |
| `MerchantSubscriptions` | `(CategoryId, TagId)` | B-tree | Reverse lookup of merchants by post category/tag |
