import type { PostStatus } from '~/composables/api/usePostsApi'

// Data model behind the Merchant Requests Feed — the `FeedItem` of the Phase 5
// API contract (planning/phase-5-merchant-response-chat.md). `useMerchantFeedApi`
// supplies it, from the backend or (behind `merchantFeedMock`) from mock data.

/** The merchant's own answer to a request (FR-RESP-1). */
export type MerchantResponse = 'HaveIt' | 'MayHaveIt' | 'CanOrderIt' | 'CantHelp'

/** Button order: best to worst availability. */
export const MERCHANT_RESPONSES: readonly MerchantResponse[] = ['HaveIt', 'MayHaveIt', 'CanOrderIt', 'CantHelp']

/** Shared by the response buttons and the response badges. */
export const RESPONSE_COLOR = {
  HaveIt: 'success',
  MayHaveIt: 'warning',
  CanOrderIt: 'info',
  CantHelp: 'error',
} as const satisfies Record<MerchantResponse, string>

export interface NamedRef {
  id: string
  name: string
}

export interface MerchantFeedRequest {
  id: string
  title: string
  description: string | null
  category: NamedRef
  tag: NamedRef
  /** Distance from the merchant's branch. */
  distanceKm: number
  /** The buyer's search radius; `null` = no radius limit. */
  buyerRadiusKm: number | null
  /** The buyer's first name. */
  buyerName: string
  isUrgent: boolean
  /** ISO timestamp; set for urgent requests. */
  urgentDeadline: string | null
  /** ISO timestamp. */
  expiresAt: string
  status: PostStatus
  /** ISO timestamp. */
  createdAt: string
  /** `null` until the merchant has answered. */
  myResponse: MerchantResponse | null
}

/** `GET /api/merchant/feed/{postId}`: the feed item plus the chat thread, once the merchant responded. */
export interface MerchantFeedDetail extends MerchantFeedRequest {
  threadId: string | null
}

/** One page of the feed; `nextCursor` is `null` on the last page. */
export interface FeedPage {
  items: MerchantFeedRequest[]
  nextCursor: string | null
}

/** `GET /api/merchant/feed/summary` — counts for the tab and navigation badges. */
export interface FeedSummary {
  newCount: number
  respondedCount: number
}

export const FEED_PAGE_SIZE = 20

export type FeedTab = 'new' | 'responded' | 'all'
export type FeedSort = 'newest' | 'nearest'

export interface FeedFilters {
  tab: FeedTab
  /** Category id; `null` = every category. */
  categoryId: string | null
  /** `null` = any distance. */
  maxDistanceKm: number | null
  sort: FeedSort
}

export const DISTANCE_OPTIONS_KM = [5, 10, 15, 25, 50] as const

export const defaultFeedFilters = (): FeedFilters => ({
  tab: 'new',
  categoryId: null,
  maxDistanceKm: null,
  sort: 'newest',
})

/** Requests the merchant has not answered yet. */
export function unansweredCount(requests: MerchantFeedRequest[]): number {
  return requests.filter(request => request.myResponse === null).length
}

/** Distinct categories, alphabetically. */
export function feedCategories(requests: MerchantFeedRequest[]): NamedRef[] {
  const byId = new Map(requests.map(request => [request.category.id, request.category]))
  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name))
}

/**
 * Applies tab, category and distance filters, then orders the result
 * (FR-FEED-2/3): "newest" lists urgent requests first, then newest first
 * within each group; "nearest" orders by distance. The backend does this
 * server-side; this is what the mock mode of `useMerchantFeedApi` emulates it with.
 */
export function filterFeed(requests: MerchantFeedRequest[], filters: FeedFilters): MerchantFeedRequest[] {
  const matching = requests.filter((request) => {
    if (filters.tab === 'new' && request.myResponse !== null) return false
    if (filters.tab === 'responded' && request.myResponse === null) return false
    if (filters.categoryId !== null && request.category.id !== filters.categoryId) return false
    if (filters.maxDistanceKm !== null && request.distanceKm > filters.maxDistanceKm) return false
    return true
  })

  return matching.sort((a, b) => {
    if (filters.sort === 'nearest') return a.distanceKm - b.distanceKm
    if (a.isUrgent !== b.isUrgent) return a.isUrgent ? -1 : 1
    return Date.parse(b.createdAt) - Date.parse(a.createdAt)
  })
}

/** "5.2 km" / "5,2 km", in the given locale. */
export function formatDistance(km: number, locale: string): string {
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(km)} km`
}

/** Query parameters of `GET /api/merchant/feed` for a filter set and cursor. */
export function feedQuery(filters: FeedFilters, cursor: string | null, limit: number = FEED_PAGE_SIZE) {
  return {
    tab: filters.tab,
    sort: filters.sort,
    categoryId: filters.categoryId ?? undefined,
    maxDistanceKm: filters.maxDistanceKm ?? undefined,
    cursor: cursor ?? undefined,
    limit,
  }
}
