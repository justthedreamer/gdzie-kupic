import {
  canRespond,
  FEED_PAGE_SIZE,
  feedCategories,
  feedQuery,
  filterFeed,
  unansweredCount,
  type FeedFilters,
  type FeedPage,
  type FeedSummary,
  type MerchantFeedDetail,
  type MerchantResponse,
  type NamedRef,
} from '~/utils/merchantFeed'

// /api/merchant/feed/* — the Merchant Requests Feed and the merchant's responses.
//
// Behind `public.merchantFeedMock` (default in dev) the same calls are
// answered from sample data instead of the backend. The mock emulates the
// server — filters, sorting, cursor paging and the summary — so the pages and
// the store behave the same in both modes. The merchant's own answers are kept
// in memory for the life of the page.
const mockResponses = new Map<string, MerchantResponse>()
/** A positive response opens a chat thread, which stays when the merchant later says "can't help". */
const mockThreads = new Map<string, string>([['feed-4', 'thread-feed-4'], ['feed-5', 'thread-feed-5']])

/**
 * Lets a test close posts while the page is open (a race with the buyer):
 * `sessionStorage['gk:mock-closed-posts'] = JSON.stringify(['feed-1'])`. A closed post stays
 * on the list, locked. Two more hooks play the server's real-time side:
 * - `gk:mock-ended-posts`: closed like above, but also gone from the list and the counters
 *   (what the server does), still readable by id;
 * - `gk:mock-hidden-posts`: not notified yet - absent everywhere until the test removes the id.
 */
function mockPostIds(key: string): string[] {
  if (!import.meta.client) return []
  try {
    return JSON.parse(sessionStorage.getItem(key) ?? '[]') as string[]
  }
  catch {
    return []
  }
}

/** Small on purpose: the sample data then spans two pages, so infinite scroll can be tried and tested. */
const MOCK_PAGE_SIZE = 4

export interface MerchantResponseResult {
  state: MerchantResponse
  threadId: string | null
  updatedAt: string
}

export const useMerchantFeedApi = () => {
  const api = useApi()
  const useMock = useRuntimeConfig().public.merchantFeedMock

  /** The sample feed; `listed` leaves out what the server no longer lists (ended posts). */
  async function mockFeed(listed = false): Promise<MerchantFeedDetail[]> {
    const { buildMockMerchantFeed } = await import('~/mocks/merchantFeed')
    const hidden = mockPostIds('gk:mock-hidden-posts')
    const ended = mockPostIds('gk:mock-ended-posts')
    const closed = [...mockPostIds('gk:mock-closed-posts'), ...ended]
    return buildMockMerchantFeed()
      .filter(request => !hidden.includes(request.id) && !(listed && ended.includes(request.id)))
      .map(request => ({
        ...request,
        status: closed.includes(request.id) ? 'Closed' as const : request.status,
        myResponse: mockResponses.get(request.id) ?? request.myResponse,
        threadId: mockThreads.get(request.id) ?? null,
      }))
  }

  return {
    /** One page of the feed; `cursor` is the `nextCursor` of the previous page. */
    list: async (filters: FeedFilters, cursor: string | null = null, limit: number = FEED_PAGE_SIZE): Promise<FeedPage> => {
      if (!useMock) {
        return api.get<FeedPage>('/api/merchant/feed', { query: feedQuery(filters, cursor, limit) })
      }

      const matching = filterFeed(await mockFeed(true), filters)
      const start = cursor === null ? 0 : Number(cursor)
      const end = start + Math.min(limit, MOCK_PAGE_SIZE)
      return { items: matching.slice(start, end), nextCursor: end < matching.length ? String(end) : null }
    },

    summary: async (): Promise<FeedSummary> => {
      if (!useMock) return api.get<FeedSummary>('/api/merchant/feed/summary')

      const all = await mockFeed(true)
      const newCount = unansweredCount(all)
      return { newCount, respondedCount: all.length - newCount }
    },

    /** Rejects with a 404 FetchError when the merchant was not notified about the post. */
    get: async (id: string): Promise<MerchantFeedDetail> => {
      if (!useMock) return api.get<MerchantFeedDetail>(`/api/merchant/feed/${id}`)

      const found = (await mockFeed()).find(request => request.id === id)
      if (!found) throw Object.assign(new Error('Not found'), { statusCode: 404 })
      return found
    },

    /** Rejects with a 409 FetchError (`post_not_active`) when the post can no longer be answered. */
    respond: async (id: string, state: MerchantResponse): Promise<MerchantResponseResult> => {
      if (!useMock) {
        return api.put<MerchantResponseResult>(`/api/merchant/feed/${id}/response`, { state })
      }

      const found = (await mockFeed()).find(request => request.id === id)
      if (!found) throw Object.assign(new Error('Not found'), { statusCode: 404 })
      if (!canRespond(found)) throw Object.assign(new Error('post_not_active'), { statusCode: 409 })

      mockResponses.set(id, state)
      if (state !== 'CantHelp') mockThreads.set(id, `thread-${id}`)
      return { state, threadId: mockThreads.get(id) ?? null, updatedAt: new Date().toISOString() }
    },

    /** Options of the category filter. */
    categories: async (): Promise<NamedRef[]> => {
      if (useMock) return feedCategories(await mockFeed())

      const categories = await useCatalogueApi().getCategories()
      return categories.map(({ id, name }) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
    },
  }
}
