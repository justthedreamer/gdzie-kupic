import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useMerchantStore } from '~/stores/merchant'

const getMe = vi.hoisted(() => vi.fn())

mockNuxtImport('useMerchantApi', () => () => ({ getMe }))

const profile = {
  merchantId: 'm1',
  name: 'Shop',
  description: null,
  branch: { id: 'b1', displayName: 'Main', latitude: 1, longitude: 2, addressDisplayName: null, phone: null, website: null },
}

describe('useMerchantStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    getMe.mockReset()
    useAuthStore().setAuth('token', { id: 'user-1', email: 'm@test', role: 'Merchant' })
  })

  it('loads the profile once and serves it from cache afterwards', async () => {
    getMe.mockResolvedValue(profile)
    const store = useMerchantStore()

    expect(await store.load()).toEqual(profile)
    expect(await store.load()).toEqual(profile)
    expect(getMe).toHaveBeenCalledOnce()
  })

  it('resolves to null (not an error) when the merchant has not onboarded', async () => {
    getMe.mockRejectedValue({ response: { status: 404 } })

    expect(await useMerchantStore().load()).toBeNull()
  })

  it('rethrows unexpected errors', async () => {
    getMe.mockRejectedValue({ response: { status: 500 } })

    await expect(useMerchantStore().load()).rejects.toBeDefined()
  })

  it('refetches when a different account signs in', async () => {
    getMe.mockResolvedValue(profile)
    const store = useMerchantStore()
    await store.load()

    useAuthStore().setAuth('token-2', { id: 'user-2', email: 'other@test', role: 'Merchant' })
    getMe.mockRejectedValue({ response: { status: 404 } })

    expect(await store.load()).toBeNull()
    expect(getMe).toHaveBeenCalledTimes(2)
  })

  it('refetches on force', async () => {
    getMe.mockResolvedValue(profile)
    const store = useMerchantStore()
    await store.load()
    await store.load(true)

    expect(getMe).toHaveBeenCalledTimes(2)
  })
})
