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
| `composables/api/usePostsApi.ts` | `/api/posts`, `/api/posts/{id}`, `.../status`, `.../fulfil`, `.../close`, `.../long-lived` | Buyer posts (requests). `list(scope)` (`active` / `ended`), `get(id)`, `status(id)` (notified / response counts, `isZeroMatch`), `fulfil(id)` and `close(id)` (204; 409 when the request is no longer active) and `makeLongLived(id)` (extends by 14 days; 409 when not eligible). `usePostStatus` polls `status()` (5 s while matching is pending, 30 s afterwards, stops on leaving the page). `create()` publishes a request (location, radius or `null` for unlimited, category + tag, title, optional description and urgent deadline) and returns the `Post`; a 400 carries the server validation message, shown on the form. Form rules (limits, radius and deadline helpers) live in `utils/postForm.ts` and mirror the server contract |
| `composables/api/useMerchantApi.ts` | `/api/merchant/me`, `/api/merchant/onboarding`, `/api/merchant/subscriptions` | Merchant profile, onboarding and category / tag subscriptions |
| `composables/api/useBuyerHomeApi.ts` | _(none yet)_ | Buyer home data. Requests come from `usePostsApi` (real posts plus live status); activity and chats have no backend until Phase 5: returns mock data when `runtimeConfig.public.buyerHomeMock` is on (default in dev, `NUXT_PUBLIC_BUYER_HOME_MOCK` overrides), otherwise an empty result. Replace the body of `load()` when the endpoints exist. |
| `composables/api/useMerchantFeedApi.ts` | `/api/merchant/feed`, `/api/merchant/feed/summary`, `/api/merchant/feed/{postId}`, `/api/merchant/feed/{postId}/response` | Merchant Requests Feed. `list(filters, cursor)` returns one cursor page (`{ items, nextCursor }`) filtered and sorted by the server (`tab`, `categoryId`, `maxDistanceKm`, `sort`), `summary()` the tab counts, `get(id)` one item with `threadId`, `respond(id, state)` the PUT of the merchant's answer (404 / 409 `post_not_active` surface as errors), `categories()` the filter options. When `runtimeConfig.public.merchantFeedMock` is on (default in dev, `NUXT_PUBLIC_MERCHANT_FEED_MOCK` overrides) the composable emulates the server in memory (paging, chat threads for positive answers, 409 for closed posts; a test can close posts by setting sessionStorage['gk:mock-closed-posts'] to a JSON array of ids); off, it calls the real endpoints. The pages talk to it only through the `useMerchantFeedStore` Pinia store (cursor paging, `useInfiniteScroll`). |
| `composables/api/useChatApi.ts` | `/api/chat/threads`, `/api/chat/threads/{id}`, `/api/chat/threads/{id}/messages`, `/api/chat/threads/{id}/read`, `/api/chat/unread-count` | Chat. `threads(cursor)` one inbox page (`{ items, nextCursor }`, newest activity first), `thread(id)`, `messages(id, { before, after, limit })` (`{ items, hasMore }`, items ascending: `before` pages back, `after` is the poll), `send(id, { body, image })` a multipart POST (`400` / `403 thread_locked` / `404` / `413 attachment_too_large` / `415 unsupported_attachment_type` surface as errors), `markRead(id)`, `unreadCount()` for the navigation badge. When `runtimeConfig.public.chatMock` is on (default in dev, `NUXT_PUBLIC_CHAT_MOCK` overrides) an in-memory server (`app/mocks/chat.ts`) answers with three sample threads per role; a test can play the other side with sessionStorage['gk:mock-chat-incoming'] (JSON `[{ "threadId", "body" }]`, delivered on the next poll) and make sending fail with sessionStorage['gk:mock-chat-fail-send'] = '1'. The pages use only the `useChatStore` Pinia store (inbox paging, per-thread cache bound to the user, polling every 4 s via `usePolling` while a thread is open, the inbox and badge every 30 s). Routes: `/chat` (inbox) and `/chat/{threadId}`. |

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
