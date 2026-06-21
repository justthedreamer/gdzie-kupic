import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('initialises unauthenticated', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
  })

  it('sets auth state on setAuth()', () => {
    const store = useAuthStore()
    store.setAuth('tok', { id: '1', email: 'a@b.com', role: 'Buyer' })
    expect(store.isAuthenticated).toBe(true)
    expect(store.user?.email).toBe('a@b.com')
  })

  it('clears auth state on clearAuth()', () => {
    const store = useAuthStore()
    store.setAuth('tok', { id: '1', email: 'a@b.com', role: 'Buyer' })
    store.clearAuth()
    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
  })

  it('isMerchant / isAdmin reflect role', () => {
    const store = useAuthStore()
    store.setAuth('tok', { id: '1', email: 'a@b.com', role: 'Merchant' })
    expect(store.isMerchant).toBe(true)
    expect(store.isAdmin).toBe(false)
  })
})
