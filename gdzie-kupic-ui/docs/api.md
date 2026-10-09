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
| `composables/api/useMerchantApi.ts` | `/api/merchant/me`, `/api/merchant/onboarding`, `/api/merchant/subscriptions` | Merchant profile, onboarding and category / tag subscriptions |
| `composables/api/useBuyerHomeApi.ts` | _(none yet)_ | Buyer home data (requests, activity, chats). No backend until Phases 4–5: returns mock data when `runtimeConfig.public.buyerHomeMock` is on (default in dev, `NUXT_PUBLIC_BUYER_HOME_MOCK` overrides), otherwise an empty result. Replace the body of `load()` when the endpoints exist. |
| `composables/api/useMerchantFeedApi.ts` | _(none yet)_ | Merchant Requests Feed (`/feed`, `/feed/{id}`). `load()` returns the feed (`MerchantFeedRequest[]`), `respond(id, state)` records the merchant's answer. No backend until Phases 4–5: returns mock data when `runtimeConfig.public.merchantFeedMock` is on (default in dev, `NUXT_PUBLIC_MERCHANT_FEED_MOCK` overrides), otherwise an empty feed; mock answers are kept in memory for the page's lifetime. The pages talk to it only through the `useMerchantFeedStore` Pinia store. Replace the bodies of `load()` and `respond()` when the endpoints exist. |

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
