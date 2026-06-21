// Low-level HTTP wrapper — see docs in FRAMEWORK.md § Data fetching
export const useApi = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string

  return {
    get: <T>(path: string, opts?: Parameters<typeof $fetch>[1]) =>
      $fetch<T>(path, { baseURL, ...opts }),

    post: <T>(path: string, body: undefined, opts?: Parameters<typeof $fetch>[1]) =>
      $fetch<T>(path, { method: 'POST', body, baseURL, ...opts }),

    put: <T>(path: string, body: undefined, opts?: Parameters<typeof $fetch>[1]) =>
      $fetch<T>(path, { method: 'PUT', body, baseURL, ...opts }),

    patch: <T>(path: string, body: undefined, opts?: Parameters<typeof $fetch>[1]) =>
      $fetch<T>(path, { method: 'PATCH', body, baseURL, ...opts }),

    del: <T>(path: string, opts?: Parameters<typeof $fetch>[1]) =>
      $fetch<T>(path, { method: 'DELETE', baseURL, ...opts }),
  }
}
