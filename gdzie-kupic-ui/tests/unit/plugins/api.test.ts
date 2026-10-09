import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import apiPlugin, { attachAuthHeader, handleAuthError } from '~/plugins/api'

describe('attachAuthHeader', () => {
  it('attaches the Authorization header when a token is present', () => {
    const headers = new Headers()
    attachAuthHeader({ headers }, 'my-token')
    expect(headers.get('Authorization')).toBe('Bearer my-token')
  })

  it('does not attach the Authorization header when there is no token', () => {
    const headers = new Headers()
    attachAuthHeader({ headers }, null)
    expect(headers.has('Authorization')).toBe(false)
  })
})

describe('handleAuthError', () => {
  it('clears auth state on a 401 response', () => {
    const clearAuth = vi.fn()
    handleAuthError({ status: 401 }, clearAuth)
    expect(clearAuth).toHaveBeenCalledOnce()
  })

  it('does not clear auth state on other error responses', () => {
    const clearAuth = vi.fn()
    handleAuthError({ status: 500 }, clearAuth)
    expect(clearAuth).not.toHaveBeenCalled()
  })
})

describe('api plugin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('provides $api and makes it the global $fetch used by useFetch()', () => {
    const { provide } = apiPlugin()

    expect(typeof provide.api).toBe('function')
    expect(globalThis.$fetch).toBe(provide.api)
  })
})
