import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useMerchantFeedStore } from '~/stores/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'
import { filterFeed, unansweredCount, type FeedPage, type MerchantFeedRequest } from '~/utils/merchantFeed'
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

describe('merchantFeed store', () => {
  let feed: MerchantFeedRequest[]

  beforeEach(() => {
    setActivePinia(createPinia())
    feed = buildMockMerchantFeed()
    api.list.mockReset().mockImplementation(serveFeed(() => feed, 2))
    api.summary.mockReset().mockImplementation(async () => {
      const newCount = unansweredCount(feed)
      return { newCount, respondedCount: feed.length - newCount }
    })
    api.get.mockReset().mockImplementation(async (id: string) => ({ ...feed.find(item => item.id === id)!, threadId: null }))
    api.respond.mockReset().mockResolvedValue({ state: 'HaveIt', threadId: null, updatedAt: '2026-01-01T00:00:00Z' })
    api.categories.mockReset().mockResolvedValue([{ id: 'cat-audio', name: 'Audio i muzyka' }])
    useAuthStore().setAuth('t', { id: 'm1', email: 'm@test', role: 'Merchant' })
  })

  describe('loading', () => {
    it('loads the first page, the summary and the categories once per user', async () => {
      const store = useMerchantFeedStore()

      await store.load()
      await store.load()

      expect(api.list).toHaveBeenCalledTimes(1)
      expect(api.list).toHaveBeenCalledWith(expect.objectContaining({ tab: 'new', sort: 'newest' }), null)
      expect(api.summary).toHaveBeenCalledTimes(1)
      expect(api.categories).toHaveBeenCalledTimes(1)
      expect(store.status).toBe('success')
      expect(store.requests).toHaveLength(2)
      expect(store.nextCursor).not.toBeNull()
      expect(store.summary).toEqual({ newCount: unansweredCount(feed), respondedCount: feed.length - unansweredCount(feed) })
      expect(store.categories).toEqual([{ id: 'cat-audio', name: 'Audio i muzyka' }])
    })

    it('reloads when forced or when another account signs in', async () => {
      const store = useMerchantFeedStore()

      await store.load()
      await store.load(true)
      useAuthStore().setAuth('t', { id: 'm2', email: 'm2@test', role: 'Merchant' })
      await store.load()

      expect(api.list).toHaveBeenCalledTimes(3)
      expect(api.summary).toHaveBeenCalledTimes(3)
    })

    it('does not show the previous account\'s data to the next one', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      await store.setFilters({ tab: 'all' })

      useAuthStore().setAuth('t', { id: 'm2', email: 'm2@test', role: 'Merchant' })
      api.list.mockRejectedValue(new Error('boom'))
      await store.load()

      expect(store.requests).toEqual([])
      expect(store.filters.tab).toBe('new')
    })

    it('reports a failed load', async () => {
      api.list.mockRejectedValue(new Error('boom'))
      const store = useMerchantFeedStore()

      await store.load()

      expect(store.status).toBe('error')
    })

    it('still shows the feed when the summary or the categories cannot be loaded', async () => {
      api.summary.mockRejectedValue(new Error('boom'))
      api.categories.mockRejectedValue(new Error('boom'))
      const store = useMerchantFeedStore()

      await store.load()

      expect(store.status).toBe('success')
      expect(store.summary).toBeNull()
      expect(store.categories).toEqual([])
    })

    it('loadSummary fetches once per user unless forced', async () => {
      const store = useMerchantFeedStore()

      await store.loadSummary()
      await store.loadSummary()
      expect(api.summary).toHaveBeenCalledTimes(1)

      await store.loadSummary(true)
      expect(api.summary).toHaveBeenCalledTimes(2)
    })
  })

  describe('filters', () => {
    it('reloads from the first page with the new filters and drops the cursor', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      await store.loadMore()
      expect(store.requests).toHaveLength(4)

      await store.setFilters({ tab: 'responded', categoryId: 'cat-audio', maxDistanceKm: 10, sort: 'nearest' })

      expect(api.list).toHaveBeenLastCalledWith(
        { tab: 'responded', categoryId: 'cat-audio', maxDistanceKm: 10, sort: 'nearest' },
        null,
      )
      expect(store.filters.tab).toBe('responded')
      expect(ids(store.requests)).toEqual(ids(filterFeed(feed, store.filters)).slice(0, 2))
    })

    it('does not refetch the summary or the categories', async () => {
      const store = useMerchantFeedStore()
      await store.load()

      await store.setFilters({ sort: 'nearest' })

      expect(api.summary).toHaveBeenCalledTimes(1)
      expect(api.categories).toHaveBeenCalledTimes(1)
    })

    it('ignores a slower response to an earlier filter', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      let release!: (page: FeedPage) => void
      api.list.mockImplementationOnce(() => new Promise<FeedPage>((resolve) => { release = resolve }))

      const slow = store.setFilters({ tab: 'all' })
      await store.setFilters({ tab: 'responded' })
      release({ items: [feed[0]!], nextCursor: null })
      await slow

      expect(store.filters.tab).toBe('responded')
      expect(ids(store.requests)).toEqual(ids(filterFeed(feed, store.filters)).slice(0, 2))
    })
  })

  describe('paging', () => {
    it('appends the next pages and stops when nextCursor is null', async () => {
      const store = useMerchantFeedStore()
      await store.setFilters({ tab: 'all' })
      expect(store.requests).toHaveLength(2)

      while (store.nextCursor !== null) await store.loadMore()

      expect(ids(store.requests)).toEqual(ids(filterFeed(feed, store.filters)))
      expect(store.requests).toHaveLength(feed.length)

      const calls = api.list.mock.calls.length
      await store.loadMore()
      expect(api.list).toHaveBeenCalledTimes(calls)
    })

    it('sends the cursor of the previous page', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const cursor = store.nextCursor

      await store.loadMore()

      expect(api.list).toHaveBeenLastCalledWith(store.filters, cursor)
    })

    it('does not start a second request while one is running', async () => {
      const store = useMerchantFeedStore()
      await store.load()

      await Promise.all([store.loadMore(), store.loadMore()])

      expect(api.list).toHaveBeenCalledTimes(2)
    })

    it('never lists a request twice when pages overlap', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const first = store.requests[0]!
      api.list.mockResolvedValueOnce({ items: [first, feed[feed.length - 1]!], nextCursor: null })

      await store.loadMore()

      expect(new Set(ids(store.requests)).size).toBe(store.requests.length)
    })

    it('keeps the list and allows a retry when loading more fails', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      api.list.mockRejectedValueOnce(new Error('boom'))

      await store.loadMore()

      expect(store.status).toBe('success')
      expect(store.requests).toHaveLength(2)
      expect(store.loadMoreFailed).toBe(true)
      expect(store.loadingMore).toBe(false)

      await store.loadMore()

      expect(store.loadMoreFailed).toBe(false)
      expect(store.requests).toHaveLength(4)
    })

    it('drops a page that arrives after the filters changed', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      let release!: (page: FeedPage) => void
      api.list.mockImplementationOnce(() => new Promise<FeedPage>((resolve) => { release = resolve }))

      const more = store.loadMore()
      await store.setFilters({ tab: 'all' })
      release({ items: [feed[0]!], nextCursor: null })
      await more

      expect(store.requests).toHaveLength(2)
      expect(store.nextCursor).not.toBeNull()
    })
  })

  describe('responding', () => {
    it('records a response and lets it be changed', async () => {
      const store = useMerchantFeedStore()
      await store.setFilters({ tab: 'all' })
      const target = store.requests.find(request => request.myResponse === null)!

      await store.respond(target.id, 'HaveIt')
      expect(store.byId(target.id)?.myResponse).toBe('HaveIt')
      expect(api.respond).toHaveBeenCalledWith(target.id, 'HaveIt')

      await store.respond(target.id, 'CantHelp')
      expect(store.byId(target.id)?.myResponse).toBe('CantHelp')
    })

    it('supports the fourth state, CanOrderIt', async () => {
      const store = useMerchantFeedStore()
      await store.setFilters({ tab: 'all' })
      const target = store.requests.find(request => request.myResponse === null)!

      await store.respond(target.id, 'CanOrderIt')

      expect(store.byId(target.id)?.myResponse).toBe('CanOrderIt')
      expect(api.respond).toHaveBeenCalledWith(target.id, 'CanOrderIt')
    })

    it('moves a request out of the New tab (but not out of reach) and updates the summary', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const before = store.summary!
      const target = store.requests[0]!

      await store.respond(target.id, 'HaveIt')

      expect(ids(store.requests)).not.toContain(target.id)
      expect(store.byId(target.id)?.myResponse).toBe('HaveIt')
      expect(store.summary).toEqual({ newCount: before.newCount - 1, respondedCount: before.respondedCount + 1 })
    })

    it('keeps a request on the Responded tab and the counts when the answer only changes', async () => {
      const store = useMerchantFeedStore()
      await store.setFilters({ tab: 'responded' })
      await store.loadSummary(true)
      const before = store.summary!
      const target = store.requests[0]!

      await store.respond(target.id, 'CanOrderIt')

      expect(store.byId(target.id)?.myResponse).toBe('CanOrderIt')
      expect(store.summary).toEqual(before)
    })

    it('re-reads the summary from the server after a response', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      api.summary.mockClear()

      await store.respond(store.requests[0]!.id, 'HaveIt')

      expect(api.summary).toHaveBeenCalledTimes(1)
    })

    it('keeps the previous answer and the summary when the API call fails', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const before = store.summary
      const target = store.requests[0]!
      api.respond.mockRejectedValue(new Error('boom'))

      await expect(store.respond(target.id, 'HaveIt')).rejects.toThrow('boom')

      expect(store.byId(target.id)?.myResponse).toBeNull()
      expect(store.summary).toEqual(before)
    })
  })

  describe('direct links', () => {
    it('fetches a request that is not on a loaded page', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const offPage = feed.find(item => !ids(store.requests).includes(item.id))!

      expect(store.byId(offPage.id)).toBeUndefined()
      await store.fetchOne(offPage.id)

      expect(api.get).toHaveBeenCalledWith(offPage.id)
      expect(store.byId(offPage.id)?.title).toBe(offPage.title)
    })

    it('rejects with the API error, e.g. a 404', async () => {
      api.get.mockRejectedValue(Object.assign(new Error('Not found'), { statusCode: 404 }))
      const store = useMerchantFeedStore()

      await expect(store.fetchOne('missing')).rejects.toThrow('Not found')
    })

    it('records a response given to a fetched request', async () => {
      const store = useMerchantFeedStore()
      await store.load()
      const offPage = feed.find(item => item.myResponse === null && !ids(store.requests).includes(item.id))!
      await store.fetchOne(offPage.id)

      await store.respond(offPage.id, 'MayHaveIt')

      expect(store.byId(offPage.id)?.myResponse).toBe('MayHaveIt')
    })
  })

  it('reset clears the cache', async () => {
    const store = useMerchantFeedStore()
    await store.load()
    await store.setFilters({ tab: 'all' })

    store.reset()

    expect(store.requests).toEqual([])
    expect(store.nextCursor).toBeNull()
    expect(store.summary).toBeNull()
    expect(store.filters.tab).toBe('new')
    expect(store.status).toBe('idle')
  })
})
