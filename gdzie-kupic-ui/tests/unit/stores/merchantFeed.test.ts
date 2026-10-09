import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useMerchantFeedStore } from '~/stores/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'

const api = vi.hoisted(() => ({ load: vi.fn(), respond: vi.fn() }))

mockNuxtImport('useMerchantFeedApi', () => () => api)

describe('merchantFeed store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    api.load.mockReset().mockImplementation(async () => buildMockMerchantFeed())
    api.respond.mockReset().mockResolvedValue(undefined)
    useAuthStore().setAuth('t', { id: 'm1', email: 'm@test', role: 'Merchant' })
  })

  it('loads the feed once per user', async () => {
    const store = useMerchantFeedStore()

    await store.load()
    await store.load()

    expect(api.load).toHaveBeenCalledTimes(1)
    expect(store.status).toBe('success')
    expect(store.requests.length).toBeGreaterThan(0)
  })

  it('reloads when forced or when another account signs in', async () => {
    const store = useMerchantFeedStore()

    await store.load()
    await store.load(true)
    useAuthStore().setAuth('t', { id: 'm2', email: 'm2@test', role: 'Merchant' })
    await store.load()

    expect(api.load).toHaveBeenCalledTimes(3)
  })

  it('reports a failed load', async () => {
    api.load.mockRejectedValue(new Error('boom'))
    const store = useMerchantFeedStore()

    await store.load()

    expect(store.status).toBe('error')
  })

  it('records a response and lets it be changed', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const target = store.requests.find(request => request.myResponse === null)!

    await store.respond(target.id, 'HaveIt')
    expect(store.byId(target.id)?.myResponse).toBe('HaveIt')
    expect(api.respond).toHaveBeenCalledWith(target.id, 'HaveIt')

    await store.respond(target.id, 'CantHelp')
    expect(store.byId(target.id)?.myResponse).toBe('CantHelp')
  })

  it('keeps the previous answer when the API call fails', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const target = store.requests.find(request => request.myResponse === null)!
    api.respond.mockRejectedValue(new Error('boom'))

    await expect(store.respond(target.id, 'HaveIt')).rejects.toThrow('boom')

    expect(store.byId(target.id)?.myResponse).toBeNull()
  })

  it('reset clears the cache', async () => {
    const store = useMerchantFeedStore()
    await store.load()

    store.reset()

    expect(store.requests).toEqual([])
    expect(store.status).toBe('idle')
  })
})
