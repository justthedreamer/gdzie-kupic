import type { LiveCounts } from '~/utils/buyerHome'

// Data model behind the Merchant Requests Feed. Posts, matching and merchant
// responses get real endpoints in Phases 4–5; until then
// `useMerchantFeedApi` supplies it.

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

export interface MerchantFeedRequest extends LiveCounts {
  id: string
  title: string
  description: string | null
  city: string
  /** Distance from the merchant's branch. */
  distanceKm: number
  /** The buyer's search radius. */
  buyerRadiusKm: number
  /** Optional, in PLN. */
  budget: number | null
  category: string
  tag: string | null
  buyerName: string
  buyerVerified: boolean
  /** ISO timestamp. */
  postedAt: string
  isUrgent: boolean
  /** ISO timestamp; set for urgent requests. */
  deadline: string | null
  /** `null` until the merchant has answered. */
  myResponse: MerchantResponse | null
}

export type FeedTab = 'new' | 'responded' | 'all'
export type FeedSort = 'newest' | 'nearest'

export interface FeedFilters {
  tab: FeedTab
  /** `null` = every category. */
  category: string | null
  /** `null` = any distance. */
  maxDistanceKm: number | null
  sort: FeedSort
}

export const DISTANCE_OPTIONS_KM = [5, 10, 15, 25, 50] as const

export const defaultFeedFilters = (): FeedFilters => ({
  tab: 'new',
  category: null,
  maxDistanceKm: null,
  sort: 'newest',
})

/** Requests the merchant has not answered yet. */
export function unansweredCount(requests: MerchantFeedRequest[]): number {
  return requests.filter(request => request.myResponse === null).length
}

/** Distinct categories, alphabetically. */
export function feedCategories(requests: MerchantFeedRequest[]): string[] {
  return [...new Set(requests.map(request => request.category))].sort((a, b) => a.localeCompare(b))
}

/**
 * Applies tab, category and distance filters, then orders the result
 * (FR-FEED-2/3): "newest" lists urgent requests first, then newest first
 * within each group; "nearest" orders by distance.
 */
export function filterFeed(requests: MerchantFeedRequest[], filters: FeedFilters): MerchantFeedRequest[] {
  const matching = requests.filter((request) => {
    if (filters.tab === 'new' && request.myResponse !== null) return false
    if (filters.tab === 'responded' && request.myResponse === null) return false
    if (filters.category !== null && request.category !== filters.category) return false
    if (filters.maxDistanceKm !== null && request.distanceKm > filters.maxDistanceKm) return false
    return true
  })

  return matching.sort((a, b) => {
    if (filters.sort === 'nearest') return a.distanceKm - b.distanceKm
    if (a.isUrgent !== b.isUrgent) return a.isUrgent ? -1 : 1
    return Date.parse(b.postedAt) - Date.parse(a.postedAt)
  })
}

/** "5.2 km" / "5,2 km", in the given locale. */
export function formatDistance(km: number, locale: string): string {
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(km)} km`
}

/** "2 000 zł" / "PLN 2,000", in the given locale. */
export function formatBudget(amount: number, locale: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency: 'PLN', maximumFractionDigits: 0 }).format(amount)
}
