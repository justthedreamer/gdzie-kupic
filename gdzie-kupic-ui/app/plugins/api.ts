// Pure interceptor logic, exported for unit testing without hitting the network.
export function attachAuthHeader(options: { headers: Headers }, token: string | null) {
  if (token) {
    options.headers.set('Authorization', `Bearer ${token}`)
  }
}

export function handleAuthError(response: { status: number }, clearAuth: () => void) {
  if (response.status === 401) {
    clearAuth()
  }
}

// Shared authenticated $fetch instance.
//
// - Attaches `Authorization: Bearer <token>` to every request when the user
//   is logged in (read live from the auth store).
// - Clears local auth state on a 401 response, so a stale/invalid token
//   doesn't cause repeated failed requests.
//
// `useApi()` consumes `$api` directly (see app/composables/useApi.ts).
// `useFetch()` picks it up automatically: Nuxt resolves its fetcher from
// `globalThis.$fetch` whenever a call doesn't pass an explicit `$fetch`
// option, so overriding it here covers both patterns without duplicating
// this logic at each call site.
export default defineNuxtPlugin(() => {
  const authStore = useAuthStore()

  const api = $fetch.create({
    onRequest: ({ options }) => attachAuthHeader(options, authStore.token),
    onResponseError: ({ response }) => handleAuthError(response, authStore.clearAuth),
  })

  globalThis.$fetch = api as typeof globalThis.$fetch

  return {
    provide: { api },
  }
})
