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

Because files in `app/composables/` are auto-imported, the composable is available anywhere without an import statement.

### Existing domain composables

| File | Endpoint | Description |
|---|---|---|
| `composables/api/useLocationApi.ts` | `GET /api/location` | Resolve GPS coords → address |

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
