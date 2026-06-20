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
| `PasswordHash` | `text` | Bcrypt |
| `Role` | `text` | `Buyer`, `MerchantAccount`, `Admin` |
| `Status` | `text` | `Active`, `Banned` |
| `CreatedAt` | `timestamptz` | |
| `BannedAt` | `timestamptz` | Nullable |

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
| `RadiusKm` | `decimal` | Buyer-defined search radius |
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
| `Channel` | `text` | `WebPush`, `Email` |
| `SentAt` | `timestamptz` | |

**Unique constraint**: `(PostId, MerchantId)` — deduplication guard

### `PushSubscriptions`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `UserId` | `uuid` | FK → Users |
| `Endpoint` | `text` | Web Push endpoint URL |
| `P256dhKey` | `text` | VAPID public key |
| `AuthKey` | `text` | VAPID auth secret |
| `CreatedAt` | `timestamptz` | |

---

## Chat

### `ChatThreads`
| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | PK |
| `PostId` | `uuid` | FK → Posts |
| `MerchantId` | `uuid` | FK → Merchants |
| `IsLocked` | `bool` | Set when buyer or merchant is banned |
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
| `Status` | `text` | `Pending`, `Processed` |
| `CreatedAt` | `timestamptz` | |
| `ProcessedAt` | `timestamptz` | Nullable |

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
| `PostNotifications` | `(PostId, MerchantId)` | Unique B-tree | Deduplication |
| `MerchantResponses` | `(PostId, MerchantId)` | Unique B-tree | One response per merchant per post |
| `ChatThreads` | `(PostId, MerchantId)` | Unique B-tree | One thread per merchant per post |
| `RefreshTokens` | `UserId` | B-tree | Token lookup on refresh |
| `MerchantSubscriptions` | `MerchantId` | B-tree | Subscription lookup during matching |
