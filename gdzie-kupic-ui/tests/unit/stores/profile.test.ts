import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useProfileStore } from '~/stores/profile'

const { get, updateFirstName } = vi.hoisted(() => ({ get: vi.fn(), updateFirstName: vi.fn() }))

mockNuxtImport('useAccountApi', () => () => ({ get, updateFirstName }))

const profile = (firstName: string | null) => ({ email: 'a@test', firstName, role: 'Buyer' })

describe('useProfileStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    get.mockReset()
    updateFirstName.mockReset()
    useAuthStore().setAuth('token', { id: 'user-1', email: 'a@test', role: 'Buyer' })
  })

  it('loads the first name once and serves it from cache afterwards', async () => {
    get.mockResolvedValue(profile('Anna'))
    const store = useProfileStore()

    expect(await store.load()).toBe('Anna')
    expect(await store.load()).toBe('Anna')
    expect(get).toHaveBeenCalledOnce()
  })

  it('refetches on force', async () => {
    get.mockResolvedValue(profile('Anna'))
    const store = useProfileStore()
    await store.load()

    get.mockResolvedValue(profile('Ewa'))

    expect(await store.load(true)).toBe('Ewa')
  })

  it('refetches, and forgets the previous name, when a different account signs in', async () => {
    get.mockResolvedValue(profile('Anna'))
    const store = useProfileStore()
    await store.load()

    useAuthStore().setAuth('token-2', { id: 'user-2', email: 'b@test', role: 'Buyer' })
    get.mockResolvedValue(profile(null))

    expect(await store.load()).toBeNull()
    expect(store.firstName).toBeNull()
    expect(get).toHaveBeenCalledTimes(2)
  })

  it('swallows load errors and leaves the name empty', async () => {
    get.mockRejectedValue({ response: { status: 500 } })

    expect(await useProfileStore().load()).toBeNull()
  })

  it('retries after a failed load', async () => {
    get.mockRejectedValueOnce({ response: { status: 500 } })
    get.mockResolvedValueOnce(profile('Anna'))
    const store = useProfileStore()

    await store.load()

    expect(await store.load()).toBe('Anna')
  })

  it('clears the name when nobody is signed in', async () => {
    get.mockResolvedValue(profile('Anna'))
    const store = useProfileStore()
    await store.load()

    useAuthStore().clearAuth()

    expect(await store.load()).toBeNull()
    expect(store.firstName).toBeNull()
  })

  it('saves a normalized name and updates the cache', async () => {
    updateFirstName.mockResolvedValue(profile('Anna Maria'))
    const store = useProfileStore()

    expect(await store.save('  Anna   Maria ')).toBe('Anna Maria')
    expect(updateFirstName).toHaveBeenCalledWith('Anna Maria')
    expect(store.firstName).toBe('Anna Maria')
    expect(await store.load()).toBe('Anna Maria')
    expect(get).not.toHaveBeenCalled()
  })

  it('clears the name by saving an empty value', async () => {
    updateFirstName.mockResolvedValue(profile(null))
    const store = useProfileStore()

    expect(await store.save('   ')).toBeNull()
    expect(updateFirstName).toHaveBeenCalledWith(null)
  })

  it('rethrows save errors and keeps the previous name', async () => {
    get.mockResolvedValue(profile('Anna'))
    const store = useProfileStore()
    await store.load()
    updateFirstName.mockRejectedValue({ response: { status: 400 } })

    await expect(store.save('Ewa')).rejects.toBeDefined()
    expect(store.firstName).toBe('Anna')
  })
})
