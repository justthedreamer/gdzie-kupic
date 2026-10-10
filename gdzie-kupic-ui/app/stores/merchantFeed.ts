import { parseApiError } from '~/utils/apiError'
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
  /** Chat thread per request id (`null` = none yet); known after a detail fetch or a response. */
  const threadIds = ref<Record<string, string | null>>({})
  /** Requests with a response in flight. */
  const responding = ref<string[]>([])

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
      threadIds.value = {}
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

  /**
   * Fetches one request from the server (a direct link, or a refresh of what is
   * already shown) and brings every cached copy up to date. Rejects with the API error.
   */
  async function fetchOne(id: string): Promise<MerchantFeedRequest> {
    const { threadId, ...item } = await useMerchantFeedApi().get(id)
    threadIds.value = { ...threadIds.value, [id]: threadId }

    const copies = [...requests.value, ...opened.value].filter(request => request.id === id)
    if (copies.length) copies.forEach(copy => Object.assign(copy, item))
    else opened.value = [...opened.value, item]

    return item
  }

  /** The chat thread of the merchant's response to a request, once known. */
  function threadIdOf(id: string): string | null {
    return threadIds.value[id] ?? null
  }

  function isResponding(id: string): boolean {
    return responding.value.includes(id)
  }

  /**
   * Sets (or changes) the merchant's answer to a request. A second call for the same
   * request while one is in flight is ignored. When the post closed in the meantime
   * (409 `post_not_active`) the cached state is refreshed before the error is rethrown.
   */
  async function respond(id: string, state: MerchantResponse): Promise<void> {
    if (isResponding(id)) return

    responding.value = [...responding.value, id]
    try {
      const result = await useMerchantFeedApi().respond(id, state)
      threadIds.value = { ...threadIds.value, [id]: result.threadId }
      applyResponse(id, state)
    }
    catch (err) {
      if (parseApiError(err).status === 409) await Promise.allSettled([fetchOne(id), loadSummary(true)])
      throw err
    }
    finally {
      responding.value = responding.value.filter(other => other !== id)
    }
  }

  function applyResponse(id: string, state: MerchantResponse): void {
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

  /**
   * Brings the feed and the counters up to date after a real-time event (`postAdded`, `resync`),
   * keeping the tab and the filters. The first page is fetched in the background and replaces
   * the list when it arrives, so nothing flickers; a failure keeps what is shown. Before the
   * first load only the counters (the navigation badge) are refreshed: the feed page loads
   * itself.
   */
  async function refresh(): Promise<void> {
    const list = status.value === 'success'
      ? refreshFirstPage()
      : status.value === 'error' ? fetchFirstPage() : Promise.resolve()

    await Promise.all([list, loadSummary(true)])
  }

  async function refreshFirstPage(): Promise<void> {
    const current = ++generation
    loadingMore.value = false
    loadMoreFailed.value = false

    try {
      const page = await useMerchantFeedApi().list(filters.value, null)
      if (current !== generation) return

      requests.value = page.items
      nextCursor.value = page.nextCursor
    }
    catch {
      // Keep the list; the next event or poll tries again.
    }
  }

  /**
   * The post was closed, fulfilled or expired (`postRemoved`). The server no longer lists it,
   * so a copy that is on screen is kept by id (the details page that shows it stays
   * usable) and refreshed to its real, no longer active status before the list is reloaded.
   */
  async function postRemoved(id: string): Promise<void> {
    const shown = requests.value.find(request => request.id === id)
    if (shown && !opened.value.some(request => request.id === id)) opened.value = [...opened.value, shown]

    if (byId(id)) await fetchOne(id).catch(() => undefined)
    await refresh()
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
    threadIds.value = {}
    responding.value = []
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
    threadIdOf,
    isResponding,
    respond,
    refresh,
    postRemoved,
    reset,
  }
})
