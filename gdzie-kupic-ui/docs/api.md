# API Layer

The project uses two patterns depending on context. Choose based on when the data is needed.

---

## Authentication

Every outgoing request — through both `useApi()` and `useFetch()` — is automatically
authenticated. This is handled by a single shared mechanism in
[`app/plugins/api.ts`](../app/plugins/api.ts), so call sites never attach the header
themselves:

- A Nuxt plugin creates an `ofetch` instance (`$api`) with an `onRequest` hook that reads
  the token from `authStore` and attaches `Authorization: Bearer <token>` whenever the
  user is logged in. Unauthenticated requests are sent as-is.
- The same instance is also assigned to `globalThis.$fetch`, which is what `useFetch()`
  resolves to internally when a call doesn't pass its own `$fetch` option — so SSR page
  data fetches are covered too, without passing anything extra at the call site.
- An `onResponseError` hook clears local auth state (`authStore.clearAuth()`) whenever
  the API responds with `401`, to avoid looping on a stale/invalid token.

---

## `useApi()` — imperative calls

Use for **mutations** (POST / PUT / PATCH / DELETE) and any fetch triggered by a user action rather than a page load.

```ts
const api = useApi()

// Mutation
await api.post('/api/requests', formData)
await api.del(`/api/requests/${id}`)

// Client-only GET (e.g. after a button click)
const result = await api.get<SomeType>('/api/endpoint')
```

The base URL is read from `NUXT_PUBLIC_API_BASE` via `useRuntimeConfig().public.apiBase`.

---

## `useFetch()` — SSR-aware data

Use for **page-level data** that should be rendered on the server and hydrated on the client.

```ts
// inside <script setup> of a page or component
const config = useRuntimeConfig()

const { data, pending, error, refresh } = await useFetch<Request[]>(
  '/api/requests',
  { baseURL: config.public.apiBase },
)
```

Key differences from `useApi()`:

| | `useApi()` | `useFetch()` |
|---|---|---|
| Runs on server | No | Yes |
| Returns `Ref<T>` | No (raw Promise) | Yes (reactive) |
| Deduplicates requests | No | Yes |
| Use for | mutations, events | page data |

---

## Domain composables

Group related API calls into a composable under `app/composables/api/`. Each file maps to one backend controller.

**Pattern:**

```ts
// app/composables/api/useRequestsApi.ts
export const useRequestsApi = () => {
  const api = useApi()

  return {
    create: (data: CreateRequest) =>
      api.post<RequestResponse>('/api/requests', data),

    update: (id: string, data: UpdateRequest) =>
      api.put<RequestResponse>(`/api/requests/${id}`, data),

    remove: (id: string) =>
      api.del(`/api/requests/${id}`),
  }
}
```

Nuxt only auto-imports top-level files of `app/composables/`, so `nuxt.config.ts` adds `imports.dirs: ['~/composables/api']`. The composables in `composables/api/` are therefore available anywhere without an import statement.

Turn API failures into user-facing messages with `resolveApiError()` from [`app/utils/apiError.ts`](../app/utils/apiError.ts): it maps status codes to i18n messages and only trusts server-provided text for 4xx responses.

### Existing domain composables

| File | Endpoint | Description |
|---|---|---|
| `composables/api/useLocationApi.ts` | `GET /api/location`, `GET /api/location/search` | Resolve GPS coords → address; resolve a typed address → coords (preview, nothing is stored) |
| `composables/api/useCatalogueApi.ts` | `/api/catalogue/categories`, `/api/admin/categories`, `/api/admin/tags` | Read the catalogue; admin create / rename / disable / enable of categories and tags |
| `composables/api/useSavedLocationsApi.ts` | `/api/saved-locations` | List, add (coordinates) and delete buyer saved locations; items carry a readable `addressDisplayName` (`null` for older entries, the UI then shows coordinates); `LocationInput` resolves an address with `/api/location/search` first and shows a detected position via `/api/location` |
| `composables/api/usePostsApi.ts` | `/api/posts`, `/api/posts/{id}`, `.../status`, `.../responses`, `.../fulfil`, `.../close`, `.../long-lived` | Buyer posts (requests). `list(scope)` (`active` / `ended`), `get(id)`, `status(id)` (notified / response counts, `isZeroMatch`; the positive answers are split into `haveItCount`, `mayHaveItCount`, `canOrderItCount`), `responses(id)` (merchants that answered positively: `merchantId`, `shopName`, `state`, `threadId`, `unreadCount`, `updatedAt`; "can't help" is never listed), `fulfil(id)` and `close(id)` (204; 409 when the request is no longer active) and `makeLongLived(id)` (extends by 14 days; 409 when not eligible). `usePostStatus` loads `status()` on mount, on `postStatusChanged` for its request and on `resync` (the buyer home and the request page also reload the request / the list then, so an ended request is reflected without a reload); it polls only as the fallback while the real-time connection is not up (5 s while matching is pending, 30 s afterwards, stops on leaving the page). `usePostResponses` loads `responses()` once the status loaded and again with every status refresh (no timer of its own), newest answer first; `RequestResponses` renders it with a link to `/chat/{threadId}`. `create()` publishes a request (location, radius or `null` for unlimited, category + tag, title, optional description and urgent deadline) and returns the `Post`; a 400 carries the server validation message, shown on the form. Form rules (limits, radius and deadline helpers) live in `utils/postForm.ts` and mirror the server contract |
| `composables/api/useMerchantApi.ts` | `/api/merchant/me`, `/api/merchant/onboarding`, `/api/merchant/subscriptions` | Merchant profile, onboarding and category / tag subscriptions |
| `composables/api/useAccountApi.ts` | `GET` / `PUT /api/account/profile` | The signed-in user's own profile: `email`, `firstName` (`null` when not set) and `role`. `updateFirstName(name \| null)` sets or clears the first name; a 400 means it broke the rules (max 50 characters; letters, spaces, hyphens, apostrophes), which `utils/firstName.ts` mirrors for the form. The first name is the only personal name merchants ever see: it replaces "Kupujący" in the feed and in chat (the server falls back to the placeholder when empty). `useProfileStore` caches it per user (loaded by the shell frame; a failure is silent and the UI falls back to the e-mail derived name); the account page is `/account`. Sign-up accepts an optional `firstName` too (`POST /auth/sign-up`; Google sign-in prefills it). Also `GET` / `PUT /api/account/notification-settings` (`{ emailEnabled }`), see "Notification settings and the banner" below |
| `composables/api/useBuyerHomeApi.ts` | _(none yet)_ | Buyer home data. Requests come from `usePostsApi` (real posts plus live status); the recent chats widget (`BuyerRecentChats`) reads the real threads from the chat store; only the activity feed has no backend: returns mock data when `runtimeConfig.public.buyerHomeMock` is on (off by default; `NUXT_PUBLIC_BUYER_HOME_MOCK` overrides), otherwise an empty result. Replace the body of `load()` when the endpoints exist. |
| `composables/api/useMerchantFeedApi.ts` | `/api/merchant/feed`, `/api/merchant/feed/summary`, `/api/merchant/feed/{postId}`, `/api/merchant/feed/{postId}/response` | Merchant Requests Feed. `list(filters, cursor)` returns one cursor page (`{ items, nextCursor }`) filtered and sorted by the server (`tab`, `categoryId`, `maxDistanceKm`, `sort`), `summary()` the tab counts, `get(id)` one item with `threadId`, `respond(id, state)` the PUT of the merchant's answer (404 / 409 `post_not_active` surface as errors), `categories()` the filter options. When `runtimeConfig.public.merchantFeedMock` is on (off by default; `NUXT_PUBLIC_MERCHANT_FEED_MOCK` overrides) the composable emulates the server in memory (paging, chat threads for positive answers, 409 for closed posts; a test can close posts by setting sessionStorage['gk:mock-closed-posts'] to a JSON array of ids; `gk:mock-ended-posts` does the same and also drops them from the list and the counters like the server, `gk:mock-hidden-posts` hides posts that were not notified yet); off, it calls the real endpoints. The pages talk to it only through the `useMerchantFeedStore` Pinia store (cursor paging, `useInfiniteScroll`). Real-time: the merchant shell (`shell/Frame.vue`) feeds the store with `postAdded` (`refresh()`: the first page is swapped in the background keeping the tab and filters, plus the counters), `postRemoved` (`postRemoved(id)`: the copy on screen is kept by id and refreshed to its real status, so an open details page shows the "no longer active" notice with the answer buttons disabled and the chat link kept, then `refresh()`) and `resync`; the details page re-fetches its request on `resync`. The shell's 30 s badge poll is unchanged (it also serves the chat badge). |
| `composables/api/useChatApi.ts` | `/api/chat/threads`, `/api/chat/threads/{id}`, `/api/chat/threads/{id}/messages`, `/api/chat/threads/{id}/read`, `/api/chat/unread-count` | Chat. `threads(cursor)` one inbox page (`{ items, nextCursor }`, newest activity first), `thread(id)`, `messages(id, { before, after, limit })` (`{ items, hasMore }`, items ascending: `before` pages back, `after` is the poll), `send(id, body, image?)` a multipart POST with the parts `body` (when not empty) and `image` (`400` / `403 thread_locked` / `404` / `413 attachment_too_large` / `415 unsupported_attachment_type` surface as errors; the store turns 413 / 415 into `too_large` / `unsupported_type` and hands text and image back to the composer), `markRead(id)`, `unreadCount()` for the navigation badge. Pictures: a message carries `attachmentUrl`, a presigned URL that expires after about 15 minutes. The composer takes one JPEG, PNG or WebP of at most 5 MB (`attachmentProblem` in `utils/chat.ts` checks type and size before uploading), shows a preview with a remove button, and a message may be a picture alone. `MessageBubble` shows the picture (click to enlarge in a modal); when it fails to load (e.g. expired URL) a fallback offers "Wczytaj ponownie", which re-fetches the latest messages (`refreshMessages`) for fresh URLs. In the inbox a message without text reads "Zdjęcie". When `runtimeConfig.public.chatMock` is on (off by default; `NUXT_PUBLIC_CHAT_MOCK` overrides) an in-memory server (`app/mocks/chat.ts`) answers with three sample threads per role; a test can play the other side with sessionStorage['gk:mock-chat-incoming'] (JSON `[{ "threadId", "body", "attachmentUrl"? }]`, delivered on the next poll) and make sending fail with sessionStorage['gk:mock-chat-fail-send'] = '1'; sessionStorage['gk:mock-chat-lock'] (JSON `["threadId", ...]`) locks threads on the next request). The pages use only the `useChatStore` Pinia store (inbox paging, per-thread cache bound to the user, polling every 4 s via `usePolling` while a thread is open, the inbox and badge every 30 s; all three pollers run with `fallbackOnly`, see "Real-time events"). Routes: `/chat` (inbox) and `/chat/{threadId}`. |

---

## Real-time events

The server pushes thin events over one SignalR connection (hub `${apiBase}/hubs/app`, contract in [`planning/phase-6-real-time.md`](../../planning/phase-6-real-time.md)). Events carry identifiers only (camelCase); the UI refetches the data through REST.

| Event | Payload | Meaning |
|---|---|---|
| `postAdded` | `{ postId }` | Merchant: a new request matches the feed |
| `postRemoved` | `{ postId }` | Merchant: a notified request was closed, fulfilled or expired |
| `postStatusChanged` | `{ postId }` | Buyer: responses or status of own request changed |
| `messageReceived` | `{ threadId, messageId }` | A new message from the other participant |
| `threadUpdated` | `{ threadId }` | A thread was created or locked / unlocked |
| `notificationRaised` | `{ kind: 'merchantResponded' \| 'newMessage', postId \| null, threadId \| null }` | A reason to notify the user |
| `resync` | _(none)_ | Local, not from the server: emitted after every successful (re)connect, including the first. Refetch what the screen shows, events may have been missed |

Building blocks (all client-only):

- `plugins/realtime.client.ts` owns the connection: opened when a token exists (after sign-in), restarted when the token changes (Dev account switcher), closed on sign-out. The JWT goes in the `access_token` query string (`accessTokenFactory`); `@microsoft/signalr` is imported lazily. The wiring (events, reconnect, `resync`) is the framework-free `createRealtimeClient` in `utils/realtimeClient.ts`; reconnect backoff is 0 / 1 / 2 / 5 / 10 / 30 s, then 30 s for ever (`utils/realtime.ts`).
- `useRealtimeStore` (`stores/realtime.ts`): `state` (`connected` \| `reconnecting` \| `disconnected`), `connected`, `on(name, handler)` and `emit`. Handler errors are logged and never stop the other subscribers.
- `useRealtimeEvent(name, handler)` subscribes for the lifetime of the calling component or effect scope and unsubscribes on unmount. Use it, not the store, in screens.
- Polling is the fallback, not a parallel channel: `usePolling(task, ms, { fallbackOnly: true })` skips ticks while `useRealtimeStore().connected` and resumes the moment the connection drops. A screen that adopts events subscribes with `useRealtimeEvent`, refetches on its events and on `resync`, and polls with `fallbackOnly`.
- Chat and notifications. `useChatRealtime` (called once by `shell/Frame.vue`, so on every page of a Buyer or Merchant): `messageReceived` and `threadUpdated` call `useChatStore().refreshInbox()` (re-reads a loaded inbox and always the unread count), except `messageReceived` of the thread that is open, which `chat/[id].vue` handles itself (`poll()` fetches the message and marks it read, so the badge never counts it); `threadUpdated` of the open thread also calls `refreshThread()` (locked state); `notificationRaised` reloads the badges and shows a toast (`notification.*` texts, Polish and English) with a button to the conversation or the request, unless the user is already on that thread (`/chat/{threadId}`) or that request (`/requests/{postId}`, `/feed/{postId}`); `resync` refreshes the inbox and the badge, and the open thread polls once and refreshes its summary. The shell badge poll, the inbox poll (`/chat`, the buyer home) and the thread poll all run with `fallbackOnly`. The toasts are rendered by the `UToaster` in `shell/Frame.vue`.

### Mock mode (`realtimeMock`)

With `runtimeConfig.public.realtimeMock` on (off by default; `NUXT_PUBLIC_REALTIME_MOCK` overrides; the mock Playwright config turns it on, the unit tests too) no connection is opened and the state stays `disconnected`, so existing polling specs keep working. A test plays the server through `window.__realtime`:

```ts
await page.evaluate(() => window.__realtime!.setState('connected'))              // also emits `resync`
await page.evaluate(() => window.__realtime!.emit('postStatusChanged', { postId })) // as if pushed by the server
await page.evaluate(() => window.__realtime!.setState('reconnecting'))
```

`getState()` and `on(name, handler)` are available too. See `tests/e2e/realtime.spec.ts`.

---

## Web Push

Notifications reach a device that has the app closed through Web Push (contract in [`planning/phase-7-push-notifications.md`](../../planning/phase-7-push-notifications.md)). The service worker (`service-worker/sw.ts`, built with `@vite-pwa/nuxt` in `injectManifest` mode) also precaches the app shell.

| Endpoint | Meaning |
|---|---|
| `GET /api/push/vapid-public-key` | The server's VAPID public key (URL-safe base64). The UI accepts a plain string or `{ publicKey }`. 404 / 501 / 503 mean "not configured": the settings switch is disabled |
| `PUT /api/push/subscription` | `{ endpoint, keys: { p256dh, auth } }`: registers (or re-assigns to the caller) this device. Idempotent |
| `DELETE /api/push/subscription` | `{ endpoint }`: removes the caller's registration of that device. Idempotent |

Push payload (built by the server in Polish): `{ kind: 'newPost' \| 'merchantResponded' \| 'newMessage', postId \| null, threadId \| null, title, body }`. A tap opens `/feed/{postId}` (`newPost`), `/requests/{postId}` (`merchantResponded`) or `/chat/{threadId}` (`newMessage`); a payload that cannot be read is ignored. While a window of the app is focused the notification is not shown (the in-app toast covers it). A tap on an open window focuses it and sends it `notification-click` (`navigateTo`, so the in-memory session survives); with no window it opens one, and the user lands on the sign-in page because the session lives in memory only.

Building blocks (client-only):

- `utils/push.ts`: pure and service-worker safe (payload parsing, notification text and target, VAPID key conversion). `utils/pushBrowser.ts`: the browser's push API behind the `PushBrowser` interface (`createBrowserPush()`), plus the test fake.
- `composables/api/usePushApi.ts`: `vapidPublicKey()`, `register(subscription)`, `remove(endpoint)`.
- `usePushStore` (`stores/push.ts`): `state` (`unknown` \| `unsupported` \| `blocked` \| `notConfigured` \| `off` \| `on`), `permission`, `busy`, `error`; `refresh()`, `enable()` and `disable()` (call them from a user action, the permission prompt needs one), `syncAfterSignIn()`, `removeOnSignOut()`.
- `plugins/push.client.ts`: after every sign-in (and account switch) of a Buyer or Merchant with the permission granted, the device is registered again for that user. `useSignOut()` calls `removeOnSignOut()` before the session ends: the registration is removed at the service (never longer than 3 s, failures are ignored); the browser keeps its subscription.

### Mock mode (`pushMock`)

With `runtimeConfig.public.pushMock` on (off by default; `NUXT_PUBLIC_PUSH_MOCK` overrides; the mock Playwright config turns it on, the unit tests too) the browser's push API is a fake. A test presets the device before the app starts and reads what the app did through `window.__push`:

```ts
await page.addInitScript(() => {
  window.__pushInit = { permission: 'granted', subscription: { endpoint: 'https://push.example.test/1', keys: { p256dh: 'p', auth: 'a' } } }
})
await page.evaluate(() => window.__push!.state.prompts) // also subscribes, unsubscribes, lastKey
```

`FakePushState` lists the fields (`supported`, `permission`, `promptResult`, `subscription`, `subscribeFails`). See `tests/e2e/push.spec.ts`.

### Notification settings and the banner

The page `/settings/notifications` (Buyer and Merchant; reached from "Ustawienia" in the buyer shell and "Powiadomienia" in the merchant shell) has two switches:

- **Push on this device**: bound to `usePushStore`. `utils/notificationSettings.ts` (`pushSwitchView(state)`) maps the store state to `{ checked, disabled, hint }`; the switch is disabled and explained when push is unsupported, not configured on the server or blocked in the browser. A failed registration shows `push-error` and leaves the switch off.
- **E-mail**: `GET` / `PUT /api/account/notification-settings` with `{ emailEnabled }` (`useAccountApi`: `notificationSettings()`, `updateNotificationSettings()`), wrapped by `useNotificationSettings()` (`loading`, `loadFailed`, `saving`, `saveFailed`, `emailEnabled`, `load()`, `setEmailEnabled()`). A failed save keeps the previous value; a failed load shows a retry. The hint differs per role (the buyer gets answers to their requests, the merchant a daily digest).

`<PushBanner />` (`components/push/Banner.vue`, on `/home` and `/feed`) invites to turn push on. `usePushBanner()` returns `{ mode, dismiss }`; `pushBannerMode(state, permission, dismissed)` gives `'enable'` (a button that calls `enable()`), `'install'` (push is unsupported on this device: a hint to add the app to the home screen) or `null` (already on, blocked, not configured, dismissed). Dismissal is remembered per device in `localStorage` (`gk:push-banner-dismissed`). The e-mail endpoints are served by the service ticket "E-mail Settings & Buyer E-mail Notifications" (#138); until it lands the UI is verified on mocks (`tests/e2e/notification-settings.spec.ts`) and `tests/e2e-real/notification-settings.real.spec.ts` skips itself when the endpoint is missing.

---

## Error handling

`$fetch` (used inside `useApi`) throws on non-2xx responses. Wrap in try/catch at the call site:

```ts
try {
  await api.post('/api/requests', formData)
  await navigateTo('/requests')
}
catch (err) {
  // err is a FetchError with err.data, err.statusCode
  errorMessage.value = 'Something went wrong'
}
```

For `useFetch`, check the returned `error` ref:

```ts
const { data, error } = await useFetch('/api/requests', { baseURL })
if (error.value) console.error(error.value.statusCode)
```
