import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useMerchantFeedStore } from '~/stores/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'
import { unansweredCount, type MerchantFeedRequest } from '~/utils/merchantFeed'
import { serveFeed } from '../support/merchantFeedServer'

const api = vi.hoisted(() => ({
  list: vi.fn(),
  summary: vi.fn(),
  get: vi.fn(),
  respond: vi.fn(),
  categories: vi.fn(),
}))

mockNuxtImport('useMerchantFeedApi', () => () => api)

const ids = (items: MerchantFeedRequest[]) => items.map(item => item.id)

// What the shell does with the real-time events goes through `refresh()` and `postRemoved()`.
describe('merchantFeed store - real-time refresh', () => {
  let feed: MerchantFeedRequest[]
  let notified: Set<string>

  // The "server": what the merchant is notified about, and what has ended.
  const visible = () => feed.filter(item => notified.has(item.id) && item.status === 'Active')

  beforeEach(() => {
    setActivePinia(createPinia())
    feed = buildMockMerchantFeed()
    notified = new Set(feed.map(item => item.id))
    api.list.mockReset().mockImplementation(serveFeed(visible, 3))
    api.summary.mockReset().mockImplementation(async () => {
      const newCount = unansweredCount(visible())
      return { newCount, respondedCount: visible().length - newCount }
    })
    api.get.mockReset().mockImplementation(async (id: string) => ({ ...feed.find(item => item.id === id)!, threadId: null }))
    api.categories.mockReset().mockResolvedValue([])
    useAuthStore().setAuth('t', { id: 'm1', email: 'm@test', role: 'Merchant' })
  })

  it('adds a new request to the feed and the counters, keeping the tab and the filters', async () => {
    notified.delete('feed-1')
    const store = useMerchantFeedStore()
    await store.load()
    await store.setFilters({ tab: 'new', sort: 'nearest' })
    expect(ids(store.requests)).not.toContain('feed-1')
    const before = store.summary!.newCount

    notified.add('feed-1')
    await store.refresh()

    expect(ids(store.requests)).toContain('feed-1')
    expect(store.summary!.newCount).toBe(before + 1)
    expect(store.filters).toMatchObject({ tab: 'new', sort: 'nearest' })
    expect(api.list).toHaveBeenLastCalledWith(expect.objectContaining({ tab: 'new', sort: 'nearest' }), null)
  })

  it('swaps the list in the background: no loading state, a failure keeps what is shown', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const shown = ids(store.requests)

    let release: () => void = () => {}
    api.list.mockImplementationOnce(() => new Promise((resolve) => {
      release = () => resolve({ items: [], nextCursor: null })
    }))
    const refreshing = store.refresh()
    expect(store.status).toBe('success')
    expect(ids(store.requests)).toEqual(shown)
    release()
    await refreshing

    api.list.mockRejectedValueOnce(new Error('boom'))
    const kept = ids(store.requests)
    await store.refresh()

    expect(store.status).toBe('success')
    expect(ids(store.requests)).toEqual(kept)
  })

  it('does not leave a started "load more" hanging', async () => {
    const store = useMerchantFeedStore()
    await store.load()

    let release: () => void = () => {}
    api.list.mockImplementationOnce(() => new Promise((resolve) => {
      release = () => resolve({ items: [], nextCursor: null })
    }))
    const more = store.loadMore()
    await store.refresh()
    release()
    await more

    expect(store.loadingMore).toBe(false)
  })

  it('only refreshes the counters before the feed was loaded', async () => {
    const store = useMerchantFeedStore()

    await store.refresh()

    expect(api.list).not.toHaveBeenCalled()
    expect(api.summary).toHaveBeenCalledTimes(1)
    expect(store.summary).not.toBeNull()
  })

  it('reloads a feed that failed to load', async () => {
    api.list.mockRejectedValueOnce(new Error('boom'))
    const store = useMerchantFeedStore()
    await store.load()
    expect(store.status).toBe('error')

    await store.refresh()

    expect(store.status).toBe('success')
    expect(store.requests.length).toBeGreaterThan(0)
  })

  it('removes an ended request from the feed and the counters', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const target = store.requests[0]!.id
    const before = store.summary!.newCount

    feed.find(item => item.id === target)!.status = 'Closed'
    await store.postRemoved(target)

    expect(ids(store.requests)).not.toContain(target)
    expect(store.summary!.newCount).toBe(before - 1)
  })

  it('keeps an ended request that is on screen readable by id, with its real status', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const target = store.requests[0]!.id
    expect(store.byId(target)?.status).toBe('Active')

    feed.find(item => item.id === target)!.status = 'Expired'
    await store.postRemoved(target)

    expect(ids(store.requests)).not.toContain(target)
    expect(store.byId(target)?.status).toBe('Expired')
  })

  it('does not fetch a request it knows nothing about', async () => {
    const store = useMerchantFeedStore()
    await store.load()

    await store.postRemoved('feed-unknown')

    expect(api.get).not.toHaveBeenCalled()
  })

  it('still refreshes when the details of the removed request cannot be fetched', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    const target = store.requests[0]!.id
    api.get.mockRejectedValue(new Error('boom'))
    feed.find(item => item.id === target)!.status = 'Closed'

    await store.postRemoved(target)

    expect(ids(store.requests)).not.toContain(target)
  })
})
