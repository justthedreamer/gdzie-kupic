import {
  defaultFeedFilters,
  type FeedFilters,
  type FeedSummary,
  type MerchantFeedRequest,
  type MerchantResponse,
  type NamedRef,
} from '~/utils/merchantFeed'

// Holds the Merchant Requests Feed so that the feed page, the request details
// page and the navigation badge share one state, and a response given on any
// of them is visible on all. Filtering, sorting and paging happen on the
// server: changing a filter reloads the first page, `loadMore()` follows the
// cursor. The cache is bound to the user id: switching accounts reloads.
export const useMerchantFeedStore = defineStore('merchantFeed', () => {
  const requests = ref<MerchantFeedRequest[]>([])
  const nextCursor = ref<string | null>(null)
  const status = ref<'idle' | 'pending' | 'success' | 'error'>('idle')
  const loadingMore = ref(false)
  const loadMoreFailed = ref(false)
  const filters = ref<FeedFilters>(defaultFeedFilters())
  /** Counts for the tab and navigation badges; `null` until the first load. */
  const summary = ref<FeedSummary | null>(null)
  const categories = ref<NamedRef[]>([])
  /** Requests opened by direct link that are not on a loaded page. */
  const opened = ref<MerchantFeedRequest[]>([])
  const loadedFor = ref<string | null>(null)
  const summaryFor = ref<string | null>(null)

  // Bumped whenever the list is replaced; a slower, older response is dropped.
  let generation = 0

  const currentUserId = () => useAuthStore().user?.id ?? null

  async function fetchFirstPage(): Promise<void> {
    const current = ++generation
    status.value = 'pending'
    loadMoreFailed.value = false
    loadingMore.value = false

    try {
      const page = await useMerchantFeedApi().list(filters.value, null)
      if (current !== generation) return

      requests.value = page.items
      nextCursor.value = page.nextCursor
      loadedFor.value = currentUserId()
      status.value = 'success'
    }
    catch {
      if (current !== generation) return
      status.value = 'error'
    }
  }

  async function loadSummary(force = false): Promise<void> {
    const userId = currentUserId()
    if (!force && summary.value && summaryFor.value === userId) return

    try {
      summary.value = await useMerchantFeedApi().summary()
      summaryFor.value = userId
    }
    catch {
      // The badges are optional; the feed itself reports its own errors.
    }
  }

  async function loadCategories(): Promise<void> {
    if (categories.value.length) return

    try {
      categories.value = await useMerchantFeedApi().categories()
    }
    catch {
      // Without the list the category filter simply offers "all categories".
    }
  }

  /** Loads the first page, the summary and the categories — once per user, unless forced. */
  async function load(force = false): Promise<void> {
    const userId = currentUserId()
    if (!force && status.value === 'success' && loadedFor.value === userId) return

    if (loadedFor.value !== userId) {
      // Another account: nothing of the previous one may stay visible.
      requests.value = []
      nextCursor.value = null
      opened.value = []
      summary.value = null
      categories.value = []
      filters.value = defaultFeedFilters()
    }

    await Promise.all([fetchFirstPage(), loadSummary(force), loadCategories()])
  }

  /** Changes the filters and reloads the feed from its first page. */
  async function setFilters(patch: Partial<FeedFilters>): Promise<void> {
    filters.value = { ...filters.value, ...patch }
    await fetchFirstPage()
  }

  /** Appends the next page (infinite scroll); a failure is kept apart from the list so it can be retried. */
  async function loadMore(): Promise<void> {
    if (status.value !== 'success' || nextCursor.value === null || loadingMore.value) return

    const current = generation
    loadingMore.value = true
    loadMoreFailed.value = false

    try {
      const page = await useMerchantFeedApi().list(filters.value, nextCursor.value)
      if (current !== generation) return

      const known = new Set(requests.value.map(request => request.id))
      requests.value = [...requests.value, ...page.items.filter(item => !known.has(item.id))]
      nextCursor.value = page.nextCursor
    }
    catch {
      if (current === generation) loadMoreFailed.value = true
    }
    finally {
      if (current === generation) loadingMore.value = false
    }
  }

  function byId(id: string): MerchantFeedRequest | undefined {
    return requests.value.find(request => request.id === id) ?? opened.value.find(request => request.id === id)
  }

  /** Fetches one request that is not on a loaded page (a direct link). Rejects with the API error. */
  async function fetchOne(id: string): Promise<MerchantFeedRequest> {
    const detail = await useMerchantFeedApi().get(id)
    opened.value = [...opened.value.filter(request => request.id !== id), detail]
    return detail
  }

  /** Sets (or changes) the merchant's answer to a request. */
  async function respond(id: string, state: MerchantResponse): Promise<void> {
    await useMerchantFeedApi().respond(id, state)

    const wasUnanswered = byId(id)?.myResponse === null
    for (const request of [...requests.value, ...opened.value]) {
      if (request.id === id) request.myResponse = state
    }

    // The request now belongs to the Responded tab: it leaves the New one
    // (but stays reachable by id, e.g. for the details page that is open).
    const leaving = filters.value.tab === 'new' ? requests.value.find(request => request.id === id) : undefined
    if (leaving) {
      requests.value = requests.value.filter(request => request.id !== id)
      opened.value = [...opened.value.filter(request => request.id !== id), leaving]
    }

    if (wasUnanswered && summary.value) {
      summary.value = {
        newCount: Math.max(0, summary.value.newCount - 1),
        respondedCount: summary.value.respondedCount + 1,
      }
    }
    void loadSummary(true)
  }

  function reset() {
    generation++
    requests.value = []
    nextCursor.value = null
    status.value = 'idle'
    loadingMore.value = false
    loadMoreFailed.value = false
    filters.value = defaultFeedFilters()
    summary.value = null
    categories.value = []
    opened.value = []
    loadedFor.value = null
    summaryFor.value = null
  }

  return {
    requests,
    nextCursor,
    status,
    loadingMore,
    loadMoreFailed,
    filters,
    summary,
    categories,
    load,
    loadSummary,
    setFilters,
    loadMore,
    byId,
    fetchOne,
    respond,
    reset,
  }
})
